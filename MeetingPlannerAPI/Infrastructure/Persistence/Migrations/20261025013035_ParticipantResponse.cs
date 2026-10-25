using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MeetingPlannerAPI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParticipantResponse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Response",
                table: "Participants",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "NoResponse");

            // Keep existing answers: the old IsAttending flag only ever meant "accepted".
            migrationBuilder.Sql("UPDATE Participants SET Response = 'Accepted' WHERE IsAttending = 1");

            migrationBuilder.DropColumn(
                name: "IsAttending",
                table: "Participants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Response",
                table: "Participants");

            migrationBuilder.AddColumn<bool>(
                name: "IsAttending",
                table: "Participants",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
