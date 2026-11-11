using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteriorDesign.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AiJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_jobs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    design_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    result_design_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pendiente"),
                    progress = table.Column<int>(type: "integer", nullable: false),
                    message = table.Column<string>(type: "text", nullable: true),
                    input = table.Column<string>(type: "jsonb", nullable: true),
                    result = table.Column<string>(type: "jsonb", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_jobs", x => x.id);
                    table.CheckConstraint("ck_ai_jobs_progress", "progress >= 0 AND progress <= 100");
                    table.CheckConstraint("ck_ai_jobs_status", "status IN ('pendiente', 'en_proceso', 'completado', 'fallido')");
                    table.CheckConstraint("ck_ai_jobs_type", "type IN ('referencia', 'chat', 'imagen')");
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_jobs_design_id",
                table: "ai_jobs",
                column: "design_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_jobs");
        }
    }
}
