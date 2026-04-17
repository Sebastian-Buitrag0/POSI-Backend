namespace POSI.Domain.Settings;

public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@posi.app";
    public string FromName { get; set; } = "POSI";
    public string BaseUrl { get; set; } = "http://localhost:5000";
}
