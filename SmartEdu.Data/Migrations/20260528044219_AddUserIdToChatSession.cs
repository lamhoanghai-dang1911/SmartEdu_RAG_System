using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartEdu.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToChatSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "ChatSessions",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatSessions");
        }
    }
}
