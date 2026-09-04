using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingScheduler.API.Migrations
{
    public partial class AddResetCodeExpiry : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResetCodeExpiresAt",
                table: "Users",
                type: "datetime(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetCodeExpiresAt",
                table: "Users");
        }
    }
}