using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POSI.Data;
using POSI.Domain.DTOs.Auth;
using POSI.Domain.Entities;
using POSI.Domain.Exceptions;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;
using Microsoft.AspNetCore.WebUtilities;

namespace POSI.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwt;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        IOptions<JwtSettings> jwt)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _jwt = jwt.Value;
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        if (await _userManager.FindByEmailAsync(request.Email) is not null)
            throw new DuplicateEmailException();

        var slug = await GenerateUniqueSlugAsync(request.BusinessName);
        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = request.BusinessName,
            Slug = slug,
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TenantId = tenant.Id,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync();
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        await EnsureRoleExistsAsync("Admin");
        await _userManager.AddToRoleAsync(user, "Admin");

        return await BuildAuthResponseAsync(user, "Admin");
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        ApplicationUser? user;
        if (request.Identifier.Contains('@'))
        {
            user = await _userManager.FindByEmailAsync(request.Identifier);
        }
        else
        {
            user = await _userManager.Users
                .FirstOrDefaultAsync(u => u.Cedula == request.Identifier);
        }

        if (user is null)
            throw new InvalidCredentialsException();

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
            throw new InvalidCredentialsException();

        if (!user.EmailConfirmed && !user.Email!.EndsWith("@local.posi"))
            throw new EmailNotVerifiedException();

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Cashier";

        return await BuildAuthResponseAsync(user, role);
    }

    public async Task<TokenResponseDto> RefreshAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == refreshToken && !r.IsRevoked)
            ?? throw new InvalidRefreshTokenException();

        if (token.ExpiresAt < DateTime.UtcNow)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
            throw new InvalidRefreshTokenException();
        }

        token.IsRevoked = true;

        var roles = await _userManager.GetRolesAsync(token.User);
        var role = roles.FirstOrDefault() ?? "Cashier";

        var newAccessToken = GenerateAccessToken(token.User, role);
        var newRefreshToken = await CreateRefreshTokenAsync(token.User.Id);

        await _db.SaveChangesAsync();

        return new TokenResponseDto(newAccessToken, newRefreshToken.Token);
    }

    public async Task LogoutAsync(string refreshToken)
    {
        var token = await _db.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == refreshToken);

        if (token is not null)
        {
            token.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    public async Task<UserDto> GetProfileAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "Cashier";

        return MapUserDto(user, role);
    }

    private async Task<AuthResponseDto> BuildAuthResponseAsync(ApplicationUser user, string role)
    {
        var accessToken = GenerateAccessToken(user, role);
        var refreshToken = await CreateRefreshTokenAsync(user.Id);
        await _db.SaveChangesAsync();

        return new AuthResponseDto(
            accessToken,
            refreshToken.Token,
            MapUserDto(user, role)
        );
    }

    private string GenerateAccessToken(ApplicationUser user, string role)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role),
            new Claim("tenantId", user.TenantId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private Task<RefreshToken> CreateRefreshTokenAsync(string userId)
    {
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        };
        _db.RefreshTokens.Add(token);
        return Task.FromResult(token);
    }

    private static UserDto MapUserDto(ApplicationUser user, string role) => new(
        user.Id,
        user.Email!,
        user.FirstName,
        user.LastName,
        role,
        user.TenantId.ToString(),
        user.CreatedAt
    );

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
            await _roleManager.CreateAsync(new IdentityRole(roleName));
    }

    private async Task<string> GenerateUniqueSlugAsync(string businessName)
    {
        var baseSlug = Regex.Replace(
            businessName.ToLower().Normalize().Trim(),
            @"[^a-z0-9]+", "-").Trim('-');

        var slug = baseSlug;
        var counter = 2;
        while (await _db.Tenants.AnyAsync(t => t.Slug == slug))
        {
            slug = $"{baseSlug}-{counter++}";
        }
        return slug;
    }

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
        if (user is null) return string.Empty;
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

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new InvalidOperationException("Usuario no encontrado.");
        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            var error = result.Errors.FirstOrDefault()?.Description
                        ?? "Error al cambiar la contraseña.";
            throw new InvalidOperationException(error);
        }
    }
}
