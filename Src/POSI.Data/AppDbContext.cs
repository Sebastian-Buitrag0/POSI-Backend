using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;

namespace POSI.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ITenantService _tenantService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantService tenantService)
        : base(options)
    {
        _tenantService = tenantService;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    private Guid? CurrentTenantId => _tenantService.GetCurrentTenantId();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole>().ToTable("roles");
        builder.Entity<IdentityUserRole<string>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<string>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<string>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<string>>().ToTable("user_tokens");

        builder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(200);
            e.Property(t => t.Slug).IsRequired().HasMaxLength(100);
            e.HasIndex(t => t.Slug).IsUnique();
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Tenant)
             .WithMany(t => t.Users)
             .HasForeignKey(u => u.TenantId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.ToTable("refresh_tokens");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.User)
             .WithMany(u => u.RefreshTokens)
             .HasForeignKey(r => r.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Product>(e =>
        {
            e.ToTable("products");
            e.HasKey(p => p.Id);
            e.Property(p => p.Name).IsRequired().HasMaxLength(200);
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Cost).HasPrecision(18, 2);
            e.HasOne(p => p.Tenant)
             .WithMany(t => t.Products)
             .HasForeignKey(p => p.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.Category)
             .WithMany(c => c.Products)
             .HasForeignKey(p => p.CategoryId)
             .OnDelete(DeleteBehavior.SetNull);
            e.HasQueryFilter(p => CurrentTenantId == null || p.TenantId == CurrentTenantId);
        });

        builder.Entity<Category>(e =>
        {
            e.ToTable("categories");
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired().HasMaxLength(100);
            e.HasOne(c => c.Tenant)
             .WithMany(t => t.Categories)
             .HasForeignKey(c => c.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(c => CurrentTenantId == null || c.TenantId == CurrentTenantId);
        });

        builder.Entity<Sale>(e =>
        {
            e.ToTable("sales");
            e.HasKey(s => s.Id);
            e.Property(s => s.SaleNumber).IsRequired().HasMaxLength(50);
            e.Property(s => s.Subtotal).HasPrecision(18, 2);
            e.Property(s => s.Tax).HasPrecision(18, 2);
            e.Property(s => s.Total).HasPrecision(18, 2);
            e.HasOne(s => s.Tenant)
             .WithMany(t => t.Sales)
             .HasForeignKey(s => s.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasMany(s => s.Items)
             .WithOne(i => i.Sale)
             .HasForeignKey(i => i.SaleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(s => CurrentTenantId == null || s.TenantId == CurrentTenantId);
        });

        builder.Entity<SaleItem>(e =>
        {
            e.ToTable("sale_items");
            e.HasKey(si => si.Id);
            e.Property(si => si.ProductName).IsRequired().HasMaxLength(200);
            e.Property(si => si.UnitPrice).HasPrecision(18, 2);
            e.Property(si => si.Subtotal).HasPrecision(18, 2);
            e.HasOne(si => si.Tenant)
             .WithMany()
             .HasForeignKey(si => si.TenantId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasQueryFilter(si => CurrentTenantId == null || si.TenantId == CurrentTenantId);
        });
    }
}
