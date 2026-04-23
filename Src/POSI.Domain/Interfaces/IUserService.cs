using POSI.Domain.DTOs.Users;

namespace POSI.Domain.Interfaces;

/// <summary>
/// Define operaciones de gestión de usuarios dentro de un tenant.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Obtiene todos los usuarios para el tenant actual.
    /// </summary>
    Task<List<TenantUserDto>> GetAllAsync();

    /// <summary>
    /// Invita a un nuevo usuario por correo.
    /// </summary>
    Task<TenantUserDto> InviteAsync(InviteUserDto dto);

    /// <summary>
    /// Crea un usuario local (sin correo) para el tenant actual.
    /// </summary>
    Task<TenantUserDto> CreateLocalAsync(CreateLocalUserDto dto);

    /// <summary>
    /// Actualiza el rol de un usuario.
    /// </summary>
    Task UpdateRoleAsync(string userId, UpdateRoleDto dto);

    /// <summary>
    /// Elimina un usuario del tenant actual.
    /// </summary>
    Task<bool> RemoveAsync(string userId, string currentUserId);
}
