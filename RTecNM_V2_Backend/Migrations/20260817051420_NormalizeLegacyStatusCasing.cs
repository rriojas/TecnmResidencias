using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TecNM.Residency.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeLegacyStatusCasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE projects SET status = lower(status) WHERE status <> lower(status);");
            migrationBuilder.Sql("UPDATE users SET role = lower(role) WHERE role <> lower(role);");
            migrationBuilder.Sql("UPDATE project_objectives SET status = lower(status) WHERE status <> lower(status);");
            migrationBuilder.Sql("UPDATE weekly_progress SET status = lower(status) WHERE status <> lower(status);");
            migrationBuilder.Sql("UPDATE documents SET status = lower(status) WHERE status <> lower(status);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
