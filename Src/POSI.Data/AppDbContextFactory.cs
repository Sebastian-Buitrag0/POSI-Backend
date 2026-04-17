using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using POSI.Domain.Interfaces;

namespace POSI.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=posi_db;Username=postgres;Password=postgres");

        return new AppDbContext(optionsBuilder.Options, new NullTenantService());
    }

    private sealed class NullTenantService : ITenantService
    {
        public Guid? GetCurrentTenantId() => null;
    }
}
