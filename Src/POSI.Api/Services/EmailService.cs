using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Api.Services;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string verificationUrl)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px">
              <h2 style="color:#3B82F6">Bienvenido a POSI</h2>
              <p>Haz clic en el siguiente enlace para verificar tu correo electrónico:</p>
              <a href="{verificationUrl}"
                 style="display:inline-block;background:#3B82F6;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                Verificar correo
              </a>
              <p style="color:#6B7280;margin-top:24px;font-size:13px">
                El enlace expira en 24 horas. Si no creaste esta cuenta, ignora este mensaje.
              </p>
            </div>
            """;

        await SendAsync(toEmail, "Verifica tu correo — POSI", html);
    }

    public async Task SendPasswordResetEmailAsync(string toEmail, string resetUrl)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px">
              <h2 style="color:#3B82F6">Restablecer contraseña</h2>
              <p>Haz clic en el enlace para crear una nueva contraseña:</p>
              <a href="{resetUrl}"
                 style="display:inline-block;background:#3B82F6;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                Restablecer contraseña
              </a>
              <p style="color:#6B7280;margin-top:24px;font-size:13px">
                El enlace expira en 1 hora. Si no solicitaste esto, ignora este mensaje.
              </p>
            </div>
            """;

        await SendAsync(toEmail, "Restablecer contraseña — POSI", html);
    }

    public async Task SendInviteEmailAsync(string toEmail, string firstName, string businessName, string tempPassword, string loginUrl)
    {
        var html = $"""
            <div style="font-family:sans-serif;max-width:480px;margin:auto;padding:32px">
              <h2 style="color:#3B82F6">Te invitaron a {businessName}</h2>
              <p>Hola {firstName}, <strong>{businessName}</strong> te ha invitado a usar POSI.</p>
              <p>Tus credenciales de acceso:</p>
              <ul>
                <li><strong>Correo:</strong> {toEmail}</li>
                <li><strong>Contraseña temporal:</strong> <code style="background:#F3F4F6;padding:4px 8px;border-radius:4px">{tempPassword}</code></li>
              </ul>
              <a href="{loginUrl}"
                 style="display:inline-block;background:#3B82F6;color:#fff;padding:12px 24px;border-radius:8px;text-decoration:none;font-weight:bold">
                Iniciar sesión
              </a>
              <p style="color:#6B7280;margin-top:24px;font-size:13px">
                Por seguridad, cambia tu contraseña después de iniciar sesión.
              </p>
            </div>
            """;
        await SendAsync(toEmail, $"Invitación a {businessName} — POSI", html);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
        {
            _logger.LogInformation("[EMAIL-DEV] To: {To} | Subject: {Subject} | Body: {Body}",
                toEmail, subject, htmlBody);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlBody };

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(_settings.SmtpUser, _settings.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {Email}", toEmail);
        }
    }
}
