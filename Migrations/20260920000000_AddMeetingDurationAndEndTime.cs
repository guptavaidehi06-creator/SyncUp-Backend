using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingScheduler.API.Migrations
{
    public partial class AddMeetingDurationAndEndTime : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Meetings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "MeetingEndTime",
                table: "Meetings",
                type: "time(6)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DurationMinutes", table: "Meetings");
            migrationBuilder.DropColumn(name: "MeetingEndTime", table: "Meetings");
        }
    }
}
