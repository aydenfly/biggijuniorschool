using BJS.Master.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Newtonsoft.Json;
using HtmlAgilityPack;

namespace BJS.Master.Pages
{
    public class IndexModel : PageModel
    {
        [BindProperty]
        public ContactModel contact { get; set; } = new();
        public string Message { get; set; }
        public bool SubmissionSuccessful { get; set; }
        private IOptions<MailjetModel> _mailjetSettings;
        private IOptions<SmtpModel> _smtpSettings;

        private readonly ILogger<IndexModel> _logger;
        public IndexModel(ILogger<IndexModel> logger, IOptions<MailjetModel> mailjetSettings, IOptions<SmtpModel> smtpSettings)
        {
            _logger = logger;
            _mailjetSettings = mailjetSettings;
            _smtpSettings = smtpSettings;
        }

        public void OnGetAsync()
        {
        }

        public async Task<JsonResult> OnPostAsync(ContactModel contact)
        {
            // If honeypot is filled, likely a bot
            if (string.IsNullOrEmpty(contact.hpToken) && ModelState.IsValid && await SendMailAsync(contact))
            {
                return new JsonResult(new
                {
                    Successful = true,
                    Message = "Thank you for your enquiry. We'll get back to you shortly."
                });

                //SubmissionSuccessful = true;
                //Message = "Thank you for your enquiry. We'll get back to you shortly.";
                //return;
            }
            return new JsonResult(new
            {
                Successful = false,
                Message = "Sorry! We couldn't process your input. Please try again."
            });

        }

        private async Task<bool> SendMailAsync(ContactModel contact)
        {
            try
            {
                //Build html content
                string htmlContent = string.Empty;
                var siteUrl = string.Format("{0}://{1}", HttpContext.Request.Scheme, HttpContext.Request.Host);
                using (var reader = new StreamReader(Path.Combine("htmlTemplates", "EmailConfirmation.html")))
                {
                    htmlContent = reader.ReadToEnd();
                }
                htmlContent = htmlContent.Replace("{SiteUrl}", siteUrl);
                htmlContent = htmlContent.Replace("{Name}", contact.fullname);
                htmlContent = htmlContent.Replace("{EmailAddress}", contact.email);
                htmlContent = htmlContent.Replace("{Message}", contact.message);
                htmlContent = htmlContent.Replace("{DateSubmitted}", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
                htmlContent = htmlContent.Replace("{Year}", DateTime.Now.Year.ToString());

                string hostName = Dns.GetHostName();
                string clientIPv4Address = Dns.GetHostAddresses(hostName).GetValue(0).ToString();
                string clientIPv6Address = Dns.GetHostEntry(hostName).AddressList.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).ToString();
                htmlContent = htmlContent.Replace("{UserIp}", !string.IsNullOrEmpty(clientIPv4Address) ? clientIPv4Address : clientIPv6Address);
                var plainTextContent = GeneratePlainText(htmlContent);

                if (_mailjetSettings.Value.UseMailjet)
                {
                    var client = new MailjetClient(_mailjetSettings.Value.ApiKey, _mailjetSettings.Value.SecretKey);

                    var email = new TransactionalEmailBuilder()
                        .WithFrom(new SendContact(_mailjetSettings.Value.FromEmail, _mailjetSettings.Value.FromAlias))
                        .WithSubject(_mailjetSettings.Value.Subject)
                        .WithHtmlPart(htmlContent)
                        .WithTextPart(plainTextContent)
                        .WithTo(new SendContact(_mailjetSettings.Value.To, _mailjetSettings.Value.ToAlias))
                        .WithReplyTo(new SendContact(_mailjetSettings.Value.FromEmail, _mailjetSettings.Value.FromAlias))
                        .Build();

                    var response = await client.SendTransactionalEmailAsync(email);
                    if (response.Messages == null || response.Messages.Length == 0 ||
                         response.Messages.Any(m => !m.Status.Equals("success", StringComparison.OrdinalIgnoreCase)))
                    {
                        throw new Exception("Mailjet email sending failed: " + JsonConvert.SerializeObject(response.Messages));
                    }
                }
                else
                {
                    var mailMsg = new MailMessage { From = new MailAddress(_smtpSettings.Value.From, _smtpSettings.Value.FromAlias) };
                    mailMsg.To.Add(new MailAddress(_smtpSettings.Value.To, _smtpSettings.Value.ToAlias));
                    mailMsg.Subject = _smtpSettings.Value.Subject;
                    mailMsg.Body = htmlContent;
                    mailMsg.ReplyToList.Add(new MailAddress(_smtpSettings.Value.From, _smtpSettings.Value.FromAlias));
                    mailMsg.IsBodyHtml = true;

                    var client = new SmtpClient
                    {
                        Host = _smtpSettings.Value.Host,
                        EnableSsl = _smtpSettings.Value.Secure,
                        Credentials = new NetworkCredential(_smtpSettings.Value.Username, _smtpSettings.Value.Password),
                        Port = _smtpSettings.Value.Port
                    };
                    await client.SendMailAsync(mailMsg);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }

            return false;
        }

        private static string GeneratePlainText(string htmlContent)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(htmlContent);
            string text = doc.GetElementbyId("TextRow").InnerText;
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

    }
}