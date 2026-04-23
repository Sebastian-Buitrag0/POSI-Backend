using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace POSI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCedulaToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cedula",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cedula",
                table: "users");
        }
    }
}
