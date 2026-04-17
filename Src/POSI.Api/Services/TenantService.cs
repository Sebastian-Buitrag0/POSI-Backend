using POSI.Domain.Interfaces;

namespace POSI.Api.Services;

public class TenantService : ITenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public TenantService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid? GetCurrentTenantId()
    {
        var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenantId");
        if (claim == null) return null;
        return Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
