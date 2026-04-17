# Spec 013-A — Email Verification + Password Reset (Backend)

## Objetivo
- Al registrar: enviar email de verificación (link que abre en browser)
- Login bloqueado si email no está verificado (403)
- Forgot password: link en email abre form HTML simple en browser
- Si SmtpHost está vacío en config → loguear el link en consola (dev mode)

## Infraestructura existente
- `ApplicationUser` extends IdentityUser — ya tiene `EmailConfirmed` (campo de Identity)
- `UserManager<ApplicationUser>` — ya tiene `GenerateEmailConfirmationTokenAsync`, `ConfirmEmailAsync`, `GeneratePasswordResetTokenAsync`, `ResetPasswordAsync`
- `AuthService` en POSI.Services — `RegisterAsync`, `LoginAsync`
- `AuthController` — 5 endpoints existentes, patrón con try-catch
- `IAuthService` en POSI.Domain/Interfaces
- Exceptions en `POSI.Domain/Exceptions/`

---

## Task 13.0 — Instalar MailKit

En `Src/POSI.Api/POSI.Api.csproj` agregar dentro de `<ItemGroup>`:
```xml
<PackageReference Include="MailKit" Version="4.8.0" />
```

---

## Task 13.1 — EmailSettings y nueva excepción

### `Src/POSI.Domain/Settings/EmailSettings.cs`
```csharp
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
```

### `Src/POSI.Domain/Exceptions/EmailNotVerifiedException.cs`
```csharp
namespace POSI.Domain.Exceptions;

public class EmailNotVerifiedException() : Exception("Email no verificado.");
```

### `Src/POSI.Domain/Interfaces/IEmailService.cs`
```csharp
namespace POSI.Domain.Interfaces;

public interface IEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationUrl);
    Task SendPasswordResetEmailAsync(string toEmail, string resetUrl);
}
```

---

## Task 13.2 — SmtpEmailService

### `Src/POSI.Api/Services/EmailService.cs`
```csharp
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

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        // Dev mode: si no hay SMTP configurado, loguear el link
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
```

---

## Task 13.3 — Modificar AuthService (LoginAsync + nuevos métodos)

En `Src/POSI.Services/AuthService.cs`:

1. Agregar using al inicio:
```csharp
using POSI.Domain.Exceptions;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;
```

2. En `LoginAsync`, agregar después de `CheckPasswordAsync`:
```csharp
if (!user.EmailConfirmed)
    throw new EmailNotVerifiedException();
```

3. Agregar estos tres nuevos métodos al final de la clase:

```csharp
public async Task<string> GetEmailVerificationTokenAsync(string userId)
{
    var user = await _userManager.FindByIdAsync(userId)
        ?? throw new InvalidOperationException("Usuario no encontrado.");
    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
}

public async Task VerifyEmailAsync(string email, string encodedToken)
{
    var user = await _userManager.FindByEmailAsync(email)
        ?? throw new InvalidOperationException("Usuario no encontrado.");
    var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
    var result = await _userManager.ConfirmEmailAsync(user, token);
    if (!result.Succeeded)
        throw new InvalidOperationException("Token inválido o expirado.");
}

public async Task<string> GetPasswordResetTokenAsync(string email)
{
    var user = await _userManager.FindByEmailAsync(email);
    if (user is null) return string.Empty; // No revelar si existe o no
    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
    return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
}

public async Task ResetPasswordAsync(string email, string encodedToken, string newPassword)
{
    var user = await _userManager.FindByEmailAsync(email)
        ?? throw new InvalidOperationException("Token inválido.");
    var token = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encodedToken));
    var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
    if (!result.Succeeded)
        throw new InvalidOperationException("Token inválido o expirado.");
}
```

---

## Task 13.4 — Actualizar IAuthService

En `Src/POSI.Domain/Interfaces/IAuthService.cs` agregar las cuatro firmas nuevas:

```csharp
Task<string> GetEmailVerificationTokenAsync(string userId);
Task VerifyEmailAsync(string email, string encodedToken);
Task<string> GetPasswordResetTokenAsync(string email);
Task ResetPasswordAsync(string email, string encodedToken, string newPassword);
```

---

## Task 13.5 — Actualizar AuthController

En `Src/POSI.Api/Controllers/AuthController.cs`:

1. Agregar campo y constructor injection:
```csharp
private readonly IEmailService _emailService;
private readonly EmailSettings _emailSettings;

public AuthController(IAuthService authService, IEmailService emailService, IOptions<EmailSettings> emailSettings)
{
    _authService = authService;
    _emailService = emailService;
    _emailSettings = emailSettings.Value;
}
```

Agregar usings:
```csharp
using Microsoft.Extensions.Options;
using POSI.Domain.Exceptions;
using POSI.Domain.Settings;
```

2. Modificar endpoint `Register` — después del `return StatusCode(201, response)` agregar envío de email:
```csharp
[HttpPost("register")]
public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
{
    try
    {
        var response = await _authService.RegisterAsync(request);
        // Enviar email de verificación (fire-and-forget, no bloquea respuesta)
        var token = await _authService.GetEmailVerificationTokenAsync(response.User.Id);
        var verifyUrl = $"{_emailSettings.BaseUrl}/api/auth/verify-email?email={Uri.EscapeDataString(response.User.Email)}&token={token}";
        _ = _emailService.SendVerificationEmailAsync(response.User.Email, verifyUrl);
        return StatusCode(201, response);
    }
    catch (DuplicateEmailException ex)
    {
        return Conflict(new { message = ex.Message });
    }
    catch (Exception ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
```

3. Modificar endpoint `Login` — agregar catch para EmailNotVerifiedException:
```csharp
catch (EmailNotVerifiedException)
{
    return StatusCode(403, new { message = "Debes verificar tu correo antes de iniciar sesión." });
}
```
Este catch va ANTES del catch genérico de Exception.

4. Agregar estos 4 endpoints nuevos al final de la clase:

```csharp
// GET /api/auth/verify-email?email=...&token=...
[HttpGet("verify-email")]
[AllowAnonymous]
public async Task<ContentResult> VerifyEmail([FromQuery] string email, [FromQuery] string token)
{
    try
    {
        await _authService.VerifyEmailAsync(email, token);
        return Content("""
            <!DOCTYPE html><html><body style="font-family:sans-serif;text-align:center;padding:60px">
            <h2 style="color:#22C55E">✓ Correo verificado</h2>
            <p>Tu cuenta está activa. Puedes cerrar esta ventana y volver a la app.</p>
            </body></html>
            """, "text/html");
    }
    catch
    {
        return Content("""
            <!DOCTYPE html><html><body style="font-family:sans-serif;text-align:center;padding:60px">
            <h2 style="color:#EF4444">✗ Enlace inválido</h2>
            <p>El enlace expiró o ya fue usado. Solicita un nuevo correo de verificación desde la app.</p>
            </body></html>
            """, "text/html");
    }
}

// POST /api/auth/resend-verification
[HttpPost("resend-verification")]
[AllowAnonymous]
public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequestDto request)
{
    try
    {
        var token = await _authService.GetEmailVerificationTokenAsync(request.UserId);
        var verifyUrl = $"{_emailSettings.BaseUrl}/api/auth/verify-email?email={Uri.EscapeDataString(request.Email)}&token={token}";
        _ = _emailService.SendVerificationEmailAsync(request.Email, verifyUrl);
        return Ok(new { message = "Email de verificación reenviado." });
    }
    catch
    {
        return Ok(new { message = "Si la cuenta existe, recibirás un email." });
    }
}

// POST /api/auth/forgot-password
[HttpPost("forgot-password")]
[AllowAnonymous]
public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
{
    var token = await _authService.GetPasswordResetTokenAsync(request.Email);
    if (!string.IsNullOrEmpty(token))
    {
        var resetUrl = $"{_emailSettings.BaseUrl}/api/auth/reset-password-page?email={Uri.EscapeDataString(request.Email)}&token={token}";
        _ = _emailService.SendPasswordResetEmailAsync(request.Email, resetUrl);
    }
    // Siempre 200 para no revelar si el email existe
    return Ok(new { message = "Si la cuenta existe, recibirás un correo." });
}

// GET /api/auth/reset-password-page?email=...&token=...  — sirve formulario HTML
[HttpGet("reset-password-page")]
[AllowAnonymous]
public ContentResult ResetPasswordPage([FromQuery] string email, [FromQuery] string token)
{
    var html = $"""
        <!DOCTYPE html>
        <html><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <title>Restablecer contraseña — POSI</title>
        <style>
          body{{font-family:sans-serif;max-width:400px;margin:60px auto;padding:24px}}
          input{{width:100%;padding:10px;margin:8px 0;border:1px solid #d1d5db;border-radius:8px;box-sizing:border-box;font-size:15px}}
          button{{width:100%;padding:12px;background:#3B82F6;color:#fff;border:none;border-radius:8px;font-size:16px;cursor:pointer;margin-top:8px}}
          button:hover{{background:#2563EB}}
          .msg{{padding:12px;border-radius:8px;margin-top:12px;display:none}}
          .ok{{background:#DCFCE7;color:#166534}}.err{{background:#FEE2E2;color:#991B1B}}
        </style></head>
        <body>
          <h2 style="color:#3B82F6">Nueva contraseña</h2>
          <form id="f">
            <input type="password" id="p1" placeholder="Nueva contraseña" minlength="6" required>
            <input type="password" id="p2" placeholder="Confirmar contraseña" minlength="6" required>
            <button type="submit">Guardar contraseña</button>
          </form>
          <div id="msg" class="msg"></div>
          <script>
            document.getElementById('f').onsubmit = async e => {{
              e.preventDefault();
              const p1 = document.getElementById('p1').value;
              const p2 = document.getElementById('p2').value;
              const msg = document.getElementById('msg');
              if (p1 !== p2) {{ msg.className='msg err'; msg.style.display='block'; msg.textContent='Las contraseñas no coinciden.'; return; }}
              const r = await fetch('/api/auth/reset-password', {{
                method:'POST', headers:{{'Content-Type':'application/json'}},
                body: JSON.stringify({{email:'{email}', token:'{token}', newPassword:p1}})
              }});
              msg.style.display='block';
              if (r.ok) {{ msg.className='msg ok'; msg.textContent='Contraseña actualizada. Puedes iniciar sesión en la app.'; document.getElementById('f').style.display='none'; }}
              else {{ const d=await r.json(); msg.className='msg err'; msg.textContent=d.message||'Error. Intenta solicitar un nuevo enlace.'; }}
            }};
          </script>
        </body></html>
        """;
    return Content(html, "text/html");
}

// POST /api/auth/reset-password  — procesa el form
[HttpPost("reset-password")]
[AllowAnonymous]
public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
{
    try
    {
        await _authService.ResetPasswordAsync(request.Email, request.Token, request.NewPassword);
        return Ok(new { message = "Contraseña actualizada." });
    }
    catch (Exception ex)
    {
        return BadRequest(new { message = ex.Message });
    }
}
```

5. Agregar DTOs al final del archivo:
```csharp
public record ResendVerificationRequestDto(string UserId, string Email);
public record ForgotPasswordRequestDto(string Email);
public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);
```

---

## Task 13.6 — Registrar servicios en Program.cs

En `Src/POSI.Api/Program.cs`, agregar después de la línea `builder.Services.Configure<JwtSettings>(...)`:

```csharp
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.AddScoped<IEmailService, EmailService>();
```

Agregar using:
```csharp
using POSI.Api.Services;
```

---

## Task 13.7 — Actualizar appsettings.json

Agregar sección `Email` en `Src/POSI.Api/appsettings.json`:

```json
"Email": {
  "SmtpHost": "",
  "SmtpPort": 587,
  "SmtpUser": "",
  "SmtpPassword": "",
  "FromEmail": "noreply@posi.app",
  "FromName": "POSI",
  "BaseUrl": "http://localhost:5000"
}
```

(SmtpHost vacío = modo dev, los links se loguean en consola)

---

## Task 13.8 — Validación

```bash
cd /Users/sebastian-buitrago/Documents/Yo/POSI/POSI-Backend
dotnet build POSI.sln
```

**0 errores, 0 warnings.**

---

## Archivos a crear
```
Src/POSI.Domain/Settings/EmailSettings.cs
Src/POSI.Domain/Exceptions/EmailNotVerifiedException.cs
Src/POSI.Domain/Interfaces/IEmailService.cs
Src/POSI.Api/Services/EmailService.cs
```

## Archivos a modificar
```
Src/POSI.Services/AuthService.cs         ← LoginAsync + 4 nuevos métodos
Src/POSI.Domain/Interfaces/IAuthService.cs ← 4 nuevas firmas
Src/POSI.Api/Controllers/AuthController.cs ← inject IEmailService + 4 endpoints + 3 DTOs
Src/POSI.Api/Program.cs                  ← registrar EmailService
Src/POSI.Api/appsettings.json            ← sección Email
Src/POSI.Api/POSI.Api.csproj             ← MailKit package
```

## IMPORTANTE — No hacer
- NO crear migraciones (EmailConfirmed ya existe en IdentityUser)
- NO modificar AppDbContext ni entidades
- El `[AllowAnonymous]` es necesario en los endpoints de verify/forgot/reset
- `GetPasswordResetTokenAsync` devuelve `string.Empty` si el usuario no existe (nunca revelar si el email está registrado)
- Los emails se envían con fire-and-forget (`_ = ...`) para no bloquear la respuesta HTTP
