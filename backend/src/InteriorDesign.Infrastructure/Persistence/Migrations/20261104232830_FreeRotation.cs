using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InteriorDesign.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FreeRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_design_objects_rotation",
                table: "design_objects");

            migrationBuilder.AddCheckConstraint(
                name: "ck_design_objects_rotation",
                table: "design_objects",
                sql: "rotation >= 0 AND rotation <= 359");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_design_objects_rotation",
                table: "design_objects");

            migrationBuilder.AddCheckConstraint(
                name: "ck_design_objects_rotation",
                table: "design_objects",
                sql: "rotation IN (0, 90, 180, 270)");
        }
    }
}
