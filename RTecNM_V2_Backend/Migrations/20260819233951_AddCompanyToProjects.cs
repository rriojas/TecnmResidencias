using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TecNM.Residency.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyToProjects : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "company_id",
                table: "projects",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "company_id",
                table: "projects",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_company_id",
                table: "projects",
                column: "company_id");

            migrationBuilder.AddForeignKey(
                name: "FK_projects_companies_company_id",
                table: "projects",
                column: "company_id",
                principalTable: "companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_projects_companies_company_id",
                table: "projects");

            migrationBuilder.DropIndex(
                name: "IX_projects_company_id",
                table: "projects");

            migrationBuilder.AlterColumn<long>(
                name: "company_id",
                table: "projects",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint");

            migrationBuilder.DropColumn(
                name: "company_id",
                table: "projects");
        }
    }
}
