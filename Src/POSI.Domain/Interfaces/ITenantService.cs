namespace POSI.Domain.Interfaces;

public interface ITenantService
{
    Guid? GetCurrentTenantId();
}
