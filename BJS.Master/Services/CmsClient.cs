using System.Net;
using System.Text;
using System.Text.Json;
using Bjs.Cms.Contracts;

namespace BJS.Master.Services;

public class CmsOptions
{
    /// <summary>Address the website server uses to reach the CMS API (can be an internal address).</summary>
    public string BaseUrl { get; set; } = "";
    /// <summary>Address visitors' browsers use for uploaded images (/uploads/...). Defaults to BaseUrl.</summary>
    public string PublicBaseUrl { get; set; } = "";
    /// <summary>Shared secret sent as X-Site-Key. Must match Site:ApiKey in the CMS.</summary>
    public string ApiKey { get; set; } = "";
    /// <summary>How long a fetched copy is used before the CMS is asked again. Edits appear within this time.</summary>
    public int CacheSeconds { get; set; } = 30;
}

public record ContactOutcome(bool Successful, string Message, Dictionary<string, string>? Errors);

/// <summary>
/// Gets the site content from the CMS API and keeps it in memory. If the CMS is unreachable the last good copy is used,
/// and if there has never been one, the snapshot shipped in App_Data/content-fallback.json, so the website stays up.
/// </summary>
public sealed class CmsClient
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly HttpClient _http;
    readonly CmsOptions _opt;
    readonly ILogger<CmsClient> _log;
    readonly string _fallbackPath;
    readonly SemaphoreSlim _gate = new(1, 1);

    PublicContent? _content;
    string? _etag;
    DateTime _freshUntilUtc = DateTime.MinValue;

    public CmsClient(HttpClient http, Microsoft.Extensions.Options.IOptions<CmsOptions> opt, ILogger<CmsClient> log, IWebHostEnvironment env)
    {
        _http = http;
        _opt = opt.Value;
        _log = log;
        _fallbackPath = Path.Combine(env.ContentRootPath, "App_Data", "content-fallback.json");
    }

    /// <summary>Base address browsers should use for files in /uploads.</summary>
    public string PublicBase => string.IsNullOrWhiteSpace(_opt.PublicBaseUrl) ? _opt.BaseUrl.TrimEnd('/') : _opt.PublicBaseUrl.TrimEnd('/');

    public async Task<PublicContent> GetAsync(CancellationToken ct = default)
    {
        if (_content != null && DateTime.UtcNow < _freshUntilUtc) return _content;

        await _gate.WaitAsync(ct);
        try
        {
            if (_content != null && DateTime.UtcNow < _freshUntilUtc) return _content;

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, _opt.BaseUrl.TrimEnd('/') + "/api/public/content");
                if (_etag != null && _content != null) req.Headers.TryAddWithoutValidation("If-None-Match", _etag);
                using var res = await _http.SendAsync(req, ct);

                if (res.StatusCode == HttpStatusCode.NotModified && _content != null)
                {
                    _freshUntilUtc = DateTime.UtcNow.AddSeconds(_opt.CacheSeconds);
                    return _content;
                }

                res.EnsureSuccessStatusCode();
                var fresh = await res.Content.ReadFromJsonAsync<PublicContent>(Json, ct);
                if (fresh == null) throw new InvalidOperationException("The CMS returned an empty response.");
                _content = fresh;
                _etag = res.Headers.ETag?.ToString();
                _freshUntilUtc = DateTime.UtcNow.AddSeconds(_opt.CacheSeconds);
                return fresh;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                _log.LogWarning(ex, "Could not load content from the CMS; using the last known copy.");
                _content ??= LoadFallback();
                _freshUntilUtc = DateTime.UtcNow.AddSeconds(10); // try again shortly, without hammering a struggling API
                return _content;
            }
        }
        finally { _gate.Release(); }
    }

    PublicContent LoadFallback()
    {
        try
        {
            if (File.Exists(_fallbackPath))
                return JsonSerializer.Deserialize<PublicContent>(File.ReadAllText(_fallbackPath), Json) ?? new PublicContent();
            _log.LogError("No fallback content found at {Path}.", _fallbackPath);
        }
        catch (Exception ex) { _log.LogError(ex, "Fallback content could not be read."); }
        return new PublicContent();
    }

    /// <summary>Forwards a contact-form submission to the CMS, which stores it and sends the email.</summary>
    public async Task<ContactOutcome> SubmitContactAsync(string? fullName, string? email, string? message, string? honeypot, string? clientIp, CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, _opt.BaseUrl.TrimEnd('/') + "/api/public/contact")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { fullName, email, message, hpToken = honeypot }, Json), Encoding.UTF8, "application/json"),
            };
            if (!string.IsNullOrEmpty(_opt.ApiKey)) req.Headers.TryAddWithoutValidation("X-Site-Key", _opt.ApiKey);
            if (!string.IsNullOrEmpty(clientIp)) req.Headers.TryAddWithoutValidation("X-Client-IP", clientIp);

            using var res = await _http.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            var root = doc.RootElement;

            var msg = root.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString()! : "";
            if (res.IsSuccessStatusCode) return new ContactOutcome(true, msg, null);

            Dictionary<string, string>? errors = null;
            if (root.TryGetProperty("errors", out var e) && e.ValueKind == JsonValueKind.Object)
                errors = e.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? "");
            if (res.StatusCode == HttpStatusCode.TooManyRequests) msg = "Too many messages sent. Please wait a few minutes and try again.";
            return new ContactOutcome(false, string.IsNullOrEmpty(msg) ? "Sorry! We couldn't process your input. Please try again." : msg, errors);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _log.LogError(ex, "Could not forward the contact form to the CMS.");
            return new ContactOutcome(false, "Sorry! We couldn't send your message right now. Please try again later.", null);
        }
    }
}
