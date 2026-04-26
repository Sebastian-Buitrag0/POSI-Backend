using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POSI.Data;
using POSI.Domain.DTOs.SuperAdmin;
using POSI.Domain.Entities;
using POSI.Domain.Interfaces;
using POSI.Domain.Settings;

namespace POSI.Services;

public class SuperAdminService : ISuperAdminService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public SuperAdminService(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        IOptions<JwtSettings> jwtOptions)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _jwtSettings = jwtOptions.Value;
    }

    public async Task<SuperAdminLoginResponseDto?> LoginAsync(SuperAdminLoginRequestDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
            return null;

        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
            return null;

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("SuperAdmin"))
            return null;

        var accessToken = GenerateJwtToken(user, roles);
        var refreshToken = await GenerateAndSaveRefreshTokenAsync(user);

        return new SuperAdminLoginResponseDto(
            accessToken,
            refreshToken,
            user.Email!,
            user.FirstName,
            user.LastName
        );
    }

    public async Task<SuperAdminDashboardDto> GetDashboardAsync()
    {
        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var totalTenants = await _db.Tenants.CountAsync();
        var activeTenants = await _db.Tenants.CountAsync(t => t.IsActive);
        var totalUsers = await _db.Users.CountAsync();
        var totalSales = await _db.Sales.CountAsync();
        var totalRevenue = await _db.Sales.SumAsync(s => s.Total);
        var tenantsCreatedThisMonth = await _db.Tenants.CountAsync(t => t.CreatedAt >= startOfMonth);
        var usersCreatedThisMonth = await _db.Users.CountAsync(u => u.CreatedAt >= startOfMonth);
        var revenueThisMonth = await _db.Sales
            .Where(s => s.CreatedAt >= startOfMonth)
            .SumAsync(s => s.Total);

        var recentTenants = await _db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Take(7)
            .Select(t => new TenantSummaryDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Plan,
                t.IsActive,
                t.Users.Count,
                t.Products.Count,
                t.Sales.Count,
                t.CreatedAt
            ))
            .ToListAsync();

        var last7Days = Enumerable.Range(0, 7)
            .Select(i => now.Date.AddDays(-i))
            .OrderBy(d => d)
            .ToList();

        var salesByDay = new List<DailyGlobalSalesDto>();
        foreach (var day in last7Days)
        {
            var nextDay = day.AddDays(1);
            var daySales = await _db.Sales
                .Where(s => s.CreatedAt >= day && s.CreatedAt < nextDay)
                .ToListAsync();

            salesByDay.Add(new DailyGlobalSalesDto(
                day.ToString("yyyy-MM-dd"),
                daySales.Sum(s => s.Total),
                daySales.Count
            ));
        }

        return new SuperAdminDashboardDto(
            totalTenants,
            activeTenants,
            totalUsers,
            totalSales,
            totalRevenue,
            tenantsCreatedThisMonth,
            usersCreatedThisMonth,
            revenueThisMonth,
            recentTenants,
            salesByDay
        );
    }

    public async Task<List<TenantSummaryDto>> GetAllTenantsAsync()
    {
        var tenants = await _db.Tenants
            .AsNoTracking()
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new TenantSummaryDto(
                t.Id,
                t.Name,
                t.Slug,
                t.Plan,
                t.IsActive,
                t.Users.Count,
                t.Products.Count,
                t.Sales.Count,
                t.CreatedAt
            ))
            .ToListAsync();

        return tenants;
    }

    public async Task<TenantDetailDto?> GetTenantByIdAsync(Guid tenantId)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .Include(t => t.Users)
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            return null;

        var users = new List<TenantUserSummaryDto>();
        foreach (var user in tenant.Users.OrderByDescending(u => u.CreatedAt))
        {
            var roles = await _userManager.GetRolesAsync(user);
            users.Add(new TenantUserSummaryDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                roles.FirstOrDefault() ?? string.Empty,
                user.CreatedAt
            ));
        }

        var productCount = await _db.Products.CountAsync(p => p.TenantId == tenantId);
        var categoryCount = await _db.Categories.CountAsync(c => c.TenantId == tenantId);
        var saleCount = await _db.Sales.CountAsync(s => s.TenantId == tenantId);
        var totalRevenue = await _db.Sales
            .Where(s => s.TenantId == tenantId)
            .SumAsync(s => s.Total);

        return new TenantDetailDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Plan,
            tenant.IsActive,
            tenant.CreatedAt,
            tenant.Users.Count,
            productCount,
            categoryCount,
            saleCount,
            totalRevenue,
            users
        );
    }

    public async Task<bool> UpdateTenantAsync(Guid tenantId, UpdateTenantDto dto)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null)
            return false;

        if (!string.IsNullOrWhiteSpace(dto.Plan))
            tenant.Plan = dto.Plan;

        if (dto.IsActive.HasValue)
            tenant.IsActive = dto.IsActive.Value;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteTenantAsync(Guid tenantId)
    {
        var tenant = await _db.Tenants.FindAsync(tenantId);
        if (tenant == null)
            return false;

        _db.Tenants.Remove(tenant);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<GlobalUserDto>> GetAllUsersAsync(int page, int pageSize)
    {
        var users = await _db.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var result = new List<GlobalUserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var tenant = await _db.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == user.TenantId);

            result.Add(new GlobalUserDto(
                user.Id,
                user.Email!,
                user.FirstName,
                user.LastName,
                roles.FirstOrDefault() ?? string.Empty,
                user.TenantId,
                tenant?.Name ?? string.Empty,
                user.CreatedAt
            ));
        }

        return result;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return false;

        var result = await _userManager.DeleteAsync(user);
        return result.Succeeded;
    }

    public async Task<TenantStatsDto?> GetTenantStatsAsync(Guid tenantId)
    {
        var tenant = await _db.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId);

        if (tenant == null)
            return null;

        var totalSales = await _db.Sales.CountAsync(s => s.TenantId == tenantId);
        var totalRevenue = await _db.Sales
            .Where(s => s.TenantId == tenantId)
            .SumAsync(s => s.Total);
        var averageTicket = totalSales > 0 ? totalRevenue / totalSales : 0;
        var productCount = await _db.Products.CountAsync(p => p.TenantId == tenantId);
        var categoryCount = await _db.Categories.CountAsync(c => c.TenantId == tenantId);
        var userCount = await _db.Users.CountAsync(u => u.TenantId == tenantId);

        var now = DateTime.UtcNow;
        var last7Days = Enumerable.Range(0, 7)
            .Select(i => now.Date.AddDays(-i))
            .OrderBy(d => d)
            .ToList();

        var salesByDay = new List<DailyGlobalSalesDto>();
        foreach (var day in last7Days)
        {
            var nextDay = day.AddDays(1);
            var daySales = await _db.Sales
                .Where(s => s.TenantId == tenantId && s.CreatedAt >= day && s.CreatedAt < nextDay)
                .ToListAsync();

            salesByDay.Add(new DailyGlobalSalesDto(
                day.ToString("yyyy-MM-dd"),
                daySales.Sum(s => s.Total),
                daySales.Count
            ));
        }

        var topProducts = await _db.SaleItems
            .AsNoTracking()
            .Where(si => si.TenantId == tenantId)
            .GroupBy(si => si.ProductName)
            .Select(g => new TopProductGlobalDto(
                g.Key,
                g.Sum(si => si.Quantity),
                g.Sum(si => si.Subtotal)
            ))
            .OrderByDescending(p => p.Quantity)
            .Take(5)
            .ToListAsync();

        return new TenantStatsDto(
            tenantId,
            tenant.Name,
            totalSales,
            totalRevenue,
            averageTicket,
            productCount,
            categoryCount,
            userCount,
            salesByDay,
            topProducts
        );
    }

    public async Task<TenantSummaryDto> CreateTenantAsync(CreateTenantDto dto)
    {
        var slug = GenerateSlug(dto.Name);

        var tenant = new Tenant
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Slug = slug,
            Plan = "free",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync();

        var adminUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = dto.AdminEmail,
            Email = dto.AdminEmail,
            FirstName = dto.AdminFirstName,
            LastName = dto.AdminLastName,
            TenantId = tenant.Id,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(adminUser, dto.AdminPassword);
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException($"Failed to create admin user: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
        }

        if (!await _roleManager.RoleExistsAsync("Admin"))
        {
            await _roleManager.CreateAsync(new IdentityRole("Admin"));
        }

        await _userManager.AddToRoleAsync(adminUser, "Admin");

        return new TenantSummaryDto(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.Plan,
            tenant.IsActive,
            1,
            0,
            0,
            tenant.CreatedAt
        );
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("tenantId", user.TenantId.ToString())
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateAndSaveRefreshTokenAsync(ApplicationUser user)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = token,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync();

        return token;
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Replace(".", "-");

        var allowed = new System.Text.StringBuilder();
        foreach (var c in slug)
        {
            if (char.IsLetterOrDigit(c) || c == '-')
                allowed.Append(c);
        }

        slug = allowed.ToString().Trim('-');
        if (string.IsNullOrEmpty(slug))
            slug = "tenant";

        return $"{slug}-{Guid.NewGuid().ToString()[..8]}";
    }
}
