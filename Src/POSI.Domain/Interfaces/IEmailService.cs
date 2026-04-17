namespace POSI.Domain.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationUrl);
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl);
    Task SendInviteEmailAsync(string toEmail, string firstName, string businessName, string tempPassword, string loginUrl);
}
