using KissakiSignup.Web.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KissakiSignup.Web.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260722190000_NormalizeRegistrationStatuses")]
public partial class NormalizeRegistrationStatuses : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("UPDATE \"Submissions\" SET \"Status\" = 2 WHERE \"Status\" IN (1, 2);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Legacy Draft and Submitted values cannot be distinguished after normalization.
    }
}
