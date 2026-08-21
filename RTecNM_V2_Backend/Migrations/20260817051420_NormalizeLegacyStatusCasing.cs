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
            migrationBuilder.Sql("DO $$ BEGIN UPDATE projects SET status = lower(status::text)::project_status WHERE status::text <> lower(status::text); EXCEPTION WHEN OTHERS THEN NULL; END $$;");
            migrationBuilder.Sql("DO $$ BEGIN UPDATE users SET role = lower(role::text)::user_role WHERE role::text <> lower(role::text); EXCEPTION WHEN OTHERS THEN NULL; END $$;");
            migrationBuilder.Sql("DO $$ BEGIN UPDATE project_objectives SET status = lower(status::text)::objective_status WHERE status::text <> lower(status::text); EXCEPTION WHEN OTHERS THEN NULL; END $$;");
            migrationBuilder.Sql("DO $$ BEGIN UPDATE weekly_progress SET status = lower(status::text)::progress_status WHERE status::text <> lower(status::text); EXCEPTION WHEN OTHERS THEN NULL; END $$;");
            migrationBuilder.Sql("DO $$ BEGIN UPDATE documents SET status = lower(status::text)::document_status WHERE status::text <> lower(status::text); EXCEPTION WHEN OTHERS THEN NULL; END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
