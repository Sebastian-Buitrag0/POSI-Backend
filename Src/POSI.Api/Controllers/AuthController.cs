using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using POSI.Domain.DTOs.Auth;
using POSI.Domain.Exceptions;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;

    public AuthController(IAuthService authService, IEmailService emailService, IOptions<EmailSettings> emailSettings)
    {
        _authService = authService;
        _emailService = emailService;
        _emailSettings = emailSettings.Value;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (InvalidCredentialsException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (EmailNotVerifiedException)
        {
            return StatusCode(403, new { message = "Debes verificar tu correo antes de iniciar sesión." });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequestDto request)
    {
        try
        {
            var response = await _authService.RefreshAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (InvalidRefreshTokenException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RefreshRequestDto? request)
    {
        if (request?.RefreshToken is not null)
            await _authService.LogoutAsync(request.RefreshToken);
        return NoContent();
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (userId is null)
            return Unauthorized(new { message = "Token inválido." });

        try
        {
            var user = await _authService.GetProfileAsync(userId);
            return Ok(user);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

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
        return Ok(new { message = "Si la cuenta existe, recibirás un correo." });
    }

    [HttpGet("reset-password-page")]
    [AllowAnonymous]
    public ContentResult ResetPasswordPage([FromQuery] string email, [FromQuery] string token)
    {
        var html = $$"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Restablecer contraseña — POSI</title>
            <style>
              body{font-family:sans-serif;max-width:400px;margin:60px auto;padding:24px}
              input{width:100%;padding:10px;margin:8px 0;border:1px solid #d1d5db;border-radius:8px;box-sizing:border-box;font-size:15px}
              button{width:100%;padding:12px;background:#3B82F6;color:#fff;border:none;border-radius:8px;font-size:16px;cursor:pointer;margin-top:8px}
              button:hover{background:#2563EB}
              .msg{padding:12px;border-radius:8px;margin-top:12px;display:none}
              .ok{background:#DCFCE7;color:#166534}.err{background:#FEE2E2;color:#991B1B}
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
                document.getElementById('f').onsubmit = async e => {
                  e.preventDefault();
                  const p1 = document.getElementById('p1').value;
                  const p2 = document.getElementById('p2').value;
                  const msg = document.getElementById('msg');
                  if (p1 !== p2) { msg.className='msg err'; msg.style.display='block'; msg.textContent='Las contraseñas no coinciden.'; return; }
                  const r = await fetch('/api/auth/reset-password', {
                    method:'POST', headers:{'Content-Type':'application/json'},
                    body: JSON.stringify({email:'{{email}}', token:'{{token}}', newPassword:p1})
                  });
                  msg.style.display='block';
                  if (r.ok) { msg.className='msg ok'; msg.textContent='Contraseña actualizada. Puedes iniciar sesión en la app.'; document.getElementById('f').style.display='none'; }
                  else { const d=await r.json(); msg.className='msg err'; msg.textContent=d.message||'Error. Intenta solicitar un nuevo enlace.'; }
                };
              </script>
            </body></html>
            """;
        return Content(html, "text/html");
    }

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
}

public record RefreshRequestDto(string RefreshToken);
public record ResendVerificationRequestDto(string UserId, string Email);
public record ForgotPasswordRequestDto(string Email);
public record ResetPasswordRequestDto(string Email, string Token, string NewPassword);
