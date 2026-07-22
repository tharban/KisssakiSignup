using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissakiSignup.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubmissionConcurrencyGuard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "Submissions",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Submissions");
        }
    }
}
