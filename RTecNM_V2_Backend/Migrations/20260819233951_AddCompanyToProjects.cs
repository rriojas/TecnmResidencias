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
            migrationBuilder.Sql(@"
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='projects' AND column_name='company_id') THEN
                        ALTER TABLE projects ADD COLUMN company_id bigint NOT NULL DEFAULT 0;
                    END IF;
                END $$;
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_projects_companies_company_id') THEN
                        ALTER TABLE projects ADD CONSTRAINT ""FK_projects_companies_company_id"" FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE RESTRICT;
                    END IF;
                END $$;
                CREATE INDEX IF NOT EXISTS ""IX_projects_company_id"" ON projects (company_id);
            ");
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
