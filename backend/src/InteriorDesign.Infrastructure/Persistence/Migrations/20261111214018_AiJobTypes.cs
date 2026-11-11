using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteriorDesign.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiJobTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_jobs_type",
                table: "ai_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_jobs_type",
                table: "ai_jobs",
                sql: "type IN ('referencia', 'chat', 'imagen', 'diseno', 'blender')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_ai_jobs_type",
                table: "ai_jobs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_ai_jobs_type",
                table: "ai_jobs",
                sql: "type IN ('referencia', 'chat', 'imagen')");
        }
    }
}
