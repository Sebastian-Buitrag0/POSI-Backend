using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSI.Data.Migrations
{
    public partial class SeedSuperAdmin : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                INSERT INTO roles (""Id"", ""Name"", ""NormalizedName"", ""ConcurrencyStamp"")
                SELECT gen_random_uuid(), 'SuperAdmin', 'SUPERADMIN', gen_random_uuid()
                WHERE NOT EXISTS (SELECT 1 FROM roles WHERE ""Name"" = 'SuperAdmin');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO user_roles (""UserId"", ""RoleId"")
                SELECT u.""Id"", r.""Id""
                FROM users u
                CROSS JOIN roles r
                WHERE u.""Email"" = 'sonysebastian2002@gmail.com'
                  AND r.""Name"" = 'SuperAdmin'
                  AND NOT EXISTS (
                      SELECT 1 FROM user_roles ur
                      WHERE ur.""UserId"" = u.""Id"" AND ur.""RoleId"" = r.""Id""
                  );
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM user_roles
                WHERE ""UserId"" = (SELECT ""Id"" FROM users WHERE ""Email"" = 'sonysebastian2002@gmail.com')
                  AND ""RoleId"" = (SELECT ""Id"" FROM roles WHERE ""Name"" = 'SuperAdmin');
            ");
        }
    }
}
