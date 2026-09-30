namespace BJS.Master.Models;

public class MailjetModel
{
    public bool UseMailjet { get; set; }
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string FromEmail { get; set; }
    public string FromAlias { get; set; }
    public string To { get; set; }
    public string ToAlias { get; set; }
    public string Subject { get; set; }
}
