using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingScheduler.API.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMandatoryToParticipant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMandatory",
                table: "MeetingParticipants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMandatory",
                table: "MeetingParticipants");
        }
    }
}
