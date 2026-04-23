using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POSI.Data;
using POSI.Domain.Constants;
using POSI.Domain.DTOs.Users;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;
using System.Security.Cryptography;

namespace POSI.Services;

/// <summary>
/// Servicio para la gestión de usuarios dentro de un tenant.
/// </summary>
public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private readonly ITenantService _tenantService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IEmailService _emailService;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<UserService> _logger;

    /// <summary>
    /// Inicializa una nueva instancia de <see cref="UserService"/>.
    /// </summary>
    /// <param name="db">Contexto de base de datos de la aplicación.</param>
    /// <param name="tenantService">Servicio para obtener el tenant actual.</param>
    /// <param name="userManager">Gestor de usuarios de Identity.</param>
    /// <param name="roleManager">Gestor de roles de Identity.</param>
    /// <param name="emailService">Servicio de envío de correos.</param>
    /// <param name="emailSettings">Configuración de correo.</param>
    /// <param name="logger">Logger para registrar eventos.</param>
    /// <exception cref="ArgumentNullException">Se lanza cuando alguna dependencia es null.</exception>
    public UserService(
        AppDbContext db,
        ITenantService tenantService,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IEmailService emailService,
        IOptions<EmailSettings> emailSettings,
        ILogger<UserService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _emailSettings = (emailSettings ?? throw new ArgumentNullException(nameof(emailSettings))).Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Obtiene todos los usuarios para el tenant actual.
    /// </summary>
    /// <returns>Lista de usuarios del tenant.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    public async Task<List<TenantUserDto>> GetAllAsync()
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var users = await _db.Users
            .Where(u => u.TenantId == tenantId.Value)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync()
            .ConfigureAwait(false);

        var result = new List<TenantUserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
            result.Add(new TenantUserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                roles.FirstOrDefault() ?? UserRoles.Default,
                user.CreatedAt));
        }

        return result;
    }

    /// <summary>
    /// Invita a un nuevo usuario por correo.
    /// </summary>
    /// <param name="dto">Datos del usuario a invitar.</param>
    /// <returns>El usuario creado.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    /// <exception cref="InvalidOperationException">Se lanza cuando se alcanza el límite de usuarios, el correo ya existe o falla la creación.</exception>
    /// <exception cref="ArgumentException">Se lanza cuando el rol es inválido.</exception>
    public async Task<TenantUserDto> InviteAsync(InviteUserDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value).ConfigureAwait(false);
        if (tenant is not null)
        {
            var userCount = await _db.Users
                .CountAsync(u => u.TenantId == tenantId.Value)
                .ConfigureAwait(false);

            if (!PlanLimits.IsWithinUserLimit(tenant.Plan, userCount))
                throw new InvalidOperationException("Has alcanzado el límite de usuarios de tu plan.");
        }

        if (await _userManager.FindByEmailAsync(dto.Email).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("Este correo ya está registrado.");

        if (!UserRoles.IsValid(dto.Role))
            throw new ArgumentException("Rol inválido.", nameof(dto.Role));

        var tempPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(9))
            .Replace("+", "A").Replace("/", "B").Replace("=", "C");

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            TenantId = tenantId.Value,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user, tempPassword).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        if (!await _roleManager.RoleExistsAsync(dto.Role).ConfigureAwait(false))
            await _roleManager.CreateAsync(new IdentityRole(dto.Role)).ConfigureAwait(false);

        await _userManager.AddToRoleAsync(user, dto.Role).ConfigureAwait(false);

        var loginUrl = _emailSettings.BaseUrl;

        async Task SendEmailAsync()
        {
            await _emailService.SendInviteEmailAsync(dto.Email, dto.FirstName, tenant?.Name ?? "POSI", tempPassword, loginUrl).ConfigureAwait(false);
        }

        FireAndForget(SendEmailAsync());

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        return new TenantUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault() ?? dto.Role,
            user.CreatedAt);
    }

    /// <summary>
    /// Crea un usuario local (sin correo real) para el tenant actual.
    /// </summary>
    /// <param name="dto">Datos del usuario local a crear.</param>
    /// <returns>El usuario creado.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    /// <exception cref="InvalidOperationException">Se lanza cuando se alcanza el límite de usuarios, la cédula ya existe o falla la creación.</exception>
    /// <exception cref="ArgumentException">Se lanza cuando el rol es inválido.</exception>
    public async Task<TenantUserDto> CreateLocalAsync(CreateLocalUserDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var tenant = await _db.Tenants.FindAsync(tenantId.Value).ConfigureAwait(false);
        if (tenant is not null)
        {
            var userCount = await _db.Users
                .CountAsync(u => u.TenantId == tenantId.Value)
                .ConfigureAwait(false);

            if (!PlanLimits.IsWithinUserLimit(tenant.Plan, userCount))
                throw new InvalidOperationException("Has alcanzado el límite de usuarios de tu plan.");
        }

        if (!UserRoles.IsValid(dto.Role))
            throw new ArgumentException("Rol inválido.", nameof(dto.Role));

        if (await _db.Users.AnyAsync(u => u.Cedula == dto.Cedula && u.TenantId == tenantId.Value).ConfigureAwait(false))
            throw new InvalidOperationException("Esta cédula ya está registrada en este negocio.");

        var user = new ApplicationUser
        {
            UserName = dto.Cedula,
            Email = $"{dto.Cedula}{LocalEmailDomain.Suffix}",
            Cedula = dto.Cedula,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            TenantId = tenantId.Value,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await _userManager.CreateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        var passwordResult = await _userManager.AddPasswordAsync(user, dto.Password).ConfigureAwait(false);
        if (!passwordResult.Succeeded)
        {
            await _userManager.DeleteAsync(user).ConfigureAwait(false);
            var errors = string.Join(", ", passwordResult.Errors.Select(e => e.Description));
            throw new InvalidOperationException(errors);
        }

        if (!await _roleManager.RoleExistsAsync(dto.Role).ConfigureAwait(false))
            await _roleManager.CreateAsync(new IdentityRole(dto.Role)).ConfigureAwait(false);

        await _userManager.AddToRoleAsync(user, dto.Role).ConfigureAwait(false);

        var roles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        return new TenantUserDto(
            user.Id,
            user.Email!,
            user.FirstName,
            user.LastName,
            roles.FirstOrDefault() ?? dto.Role,
            user.CreatedAt);
    }

    /// <summary>
    /// Actualiza el rol de un usuario.
    /// </summary>
    /// <param name="userId">Identificador del usuario.</param>
    /// <param name="dto">Datos con el nuevo rol.</param>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    /// <exception cref="KeyNotFoundException">Se lanza cuando el usuario no existe.</exception>
    /// <exception cref="ArgumentException">Se lanza cuando el rol es inválido.</exception>
    public async Task UpdateRoleAsync(string userId, UpdateRoleDto dto)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId.Value)
            .ConfigureAwait(false);

        if (user is null)
            throw new KeyNotFoundException();

        if (!UserRoles.IsValid(dto.Role))
            throw new ArgumentException("Rol inválido.", nameof(dto.Role));

        var currentRoles = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        await _userManager.RemoveFromRolesAsync(user, currentRoles).ConfigureAwait(false);

        if (!await _roleManager.RoleExistsAsync(dto.Role).ConfigureAwait(false))
            await _roleManager.CreateAsync(new IdentityRole(dto.Role)).ConfigureAwait(false);

        await _userManager.AddToRoleAsync(user, dto.Role).ConfigureAwait(false);
    }

    /// <summary>
    /// Elimina un usuario del tenant actual.
    /// </summary>
    /// <param name="userId">Identificador del usuario a eliminar.</param>
    /// <param name="currentUserId">Identificador del usuario que realiza la acción.</param>
    /// <returns>true si el usuario fue eliminado; false si no se encontró.</returns>
    /// <exception cref="UnauthorizedAccessException">Se lanza cuando no se puede determinar el tenant actual.</exception>
    /// <exception cref="InvalidOperationException">Se lanza cuando el usuario intenta eliminarse a sí mismo.</exception>
    public async Task<bool> RemoveAsync(string userId, string currentUserId)
    {
        var tenantId = _tenantService.GetCurrentTenantId();
        if (tenantId is null)
            throw new UnauthorizedAccessException();

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId.Value)
            .ConfigureAwait(false);

        if (user is null)
            return false;

        if (user.Id == currentUserId)
            throw new InvalidOperationException("No puedes eliminarte a ti mismo.");

        await _userManager.DeleteAsync(user).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Ejecuta una tarea en segundo plano y registra errores sin bloquear el flujo principal.
    /// </summary>
    /// <param name="task">Tarea a ejecutar.</param>
    private void FireAndForget(Task task)
    {
        _ = task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception is not null)
            {
                _logger.LogError(t.Exception, "Error en tarea en segundo plano.");
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
}
