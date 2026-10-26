using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bjs.Cms.Api.Services;

public record MailMessageData(string ToEmail, string ToName, string Subject, string Html, string Text, string ReplyToEmail, string ReplyToName);

/// <summary>Sends the contact-form email through Mailjet's HTTP API or plain SMTP, whichever is configured (Mail:Provider).</summary>
public sealed class MailService
{
    readonly IConfiguration _config;
    readonly IHttpClientFactory _http;
    readonly ILogger<MailService> _log;
    readonly string _template;

    public MailService(IConfiguration config, IHttpClientFactory http, ILogger<MailService> log)
    {
        _config = config; _http = http; _log = log;
        var path = Path.Combine(AppContext.BaseDirectory, "EmailTemplates", "Enquiry.html");
        _template = File.Exists(path) ? File.ReadAllText(path) : "<p>{Name} ({EmailAddress}) wrote:</p><p>{Message}</p>";
    }

    public string Provider => (_config["Mail:Provider"] ?? "None").Trim();
    public bool IsConfigured => Provider.Equals("Mailjet", StringComparison.OrdinalIgnoreCase) || Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase);

    static string E(string s) => WebUtility.HtmlEncode(s);

    /// <summary>Builds the HTML and plain-text bodies. Every visitor-supplied value is HTML-encoded.</summary>
    public (string Html, string Text) Render(string siteUrl, string name, string email, string message, DateTime whenUtc)
    {
        var html = _template
            .Replace("{SiteUrl}", E(siteUrl))
            .Replace("{Name}", E(name))
            .Replace("{EmailAddress}", E(email))
            .Replace("{Message}", E(message).Replace("\n", "<br>"))
            .Replace("{DateSubmitted}", whenUtc.ToString("dd/MM/yyyy HH:mm") + " UTC")
            .Replace("{Year}", whenUtc.Year.ToString());
        var text = $"New enquiry from {name} <{email}>\n\n{message}\n\nReceived {whenUtc:dd/MM/yyyy HH:mm} UTC";
        return (html, text);
    }

    public async Task<bool> SendAsync(MailMessageData m, CancellationToken ct = default)
    {
        try
        {
            if (Provider.Equals("Mailjet", StringComparison.OrdinalIgnoreCase)) return await SendMailjetAsync(m, ct);
            if (Provider.Equals("Smtp", StringComparison.OrdinalIgnoreCase)) return await SendSmtpAsync(m, ct);
            _log.LogWarning("Mail:Provider is '{Provider}', so the enquiry was saved but no email was sent.", Provider);
            return false;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Sending enquiry email failed.");
            return false;
        }
    }

    async Task<bool> SendMailjetAsync(MailMessageData m, CancellationToken ct)
    {
        var key = _config["Mail:Mailjet:ApiKey"]; var secret = _config["Mail:Mailjet:SecretKey"];
        var from = _config["Mail:Mailjet:FromEmail"]; var fromName = _config["Mail:Mailjet:FromName"] ?? "Website";
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(from))
        { _log.LogError("Mailjet is selected but ApiKey, SecretKey or FromEmail is missing."); return false; }

        var payload = new
        {
            Messages = new[]
            {
                new
                {
                    From = new { Email = from, Name = fromName },
                    To = new[] { new { Email = m.ToEmail, Name = m.ToName } },
                    ReplyTo = new { Email = m.ReplyToEmail, Name = m.ReplyToName },
                    Subject = m.Subject, TextPart = m.Text, HTMLPart = m.Html,
                }
            }
        };

        var client = _http.CreateClient("mailjet");
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.mailjet.com/v3.1/send")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}")));
        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) { _log.LogError("Mailjet returned {Status}: {Body}", (int)res.StatusCode, body); return false; }

        using var doc = JsonDocument.Parse(body);
        var ok = doc.RootElement.TryGetProperty("Messages", out var msgs) && msgs.GetArrayLength() > 0 &&
                 msgs.EnumerateArray().All(x => x.TryGetProperty("Status", out var s) && s.GetString()?.Equals("success", StringComparison.OrdinalIgnoreCase) == true);
        if (!ok) _log.LogError("Mailjet did not accept the message: {Body}", body);
        return ok;
    }

    async Task<bool> SendSmtpAsync(MailMessageData m, CancellationToken ct)
    {
        var host = _config["Mail:Smtp:Host"];
        var from = _config["Mail:Smtp:FromEmail"]; var fromName = _config["Mail:Smtp:FromName"] ?? "Website";
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(from)) { _log.LogError("SMTP is selected but Host or FromEmail is missing."); return false; }

        using var msg = new MailMessage { From = new MailAddress(from, fromName), Subject = m.Subject, IsBodyHtml = true, Body = m.Html };
        msg.To.Add(new MailAddress(m.ToEmail, m.ToName));
        msg.ReplyToList.Add(new MailAddress(m.ReplyToEmail, m.ReplyToName));
        msg.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(m.Text, Encoding.UTF8, "text/plain"));

        using var client = new SmtpClient(host, int.TryParse(_config["Mail:Smtp:Port"], out var port) ? port : 25)
        {
            EnableSsl = bool.TryParse(_config["Mail:Smtp:Secure"], out var secure) && secure,
        };
        var user = _config["Mail:Smtp:Username"];
        if (!string.IsNullOrEmpty(user)) client.Credentials = new NetworkCredential(user, _config["Mail:Smtp:Password"]);
        await client.SendMailAsync(msg, ct);
        return true;
    }
}
