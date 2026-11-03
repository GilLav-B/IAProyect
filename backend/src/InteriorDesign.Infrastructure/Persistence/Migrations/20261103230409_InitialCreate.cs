using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InteriorDesign.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_projects", x => x.id);
                    table.CheckConstraint("ck_projects_type", "type IN ('comercial', 'domestico')");
                });

            migrationBuilder.CreateTable(
                name: "providers",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    budget_category = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    adapter = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_providers", x => x.id);
                    table.CheckConstraint("ck_providers_adapter", "adapter IN ('json', 'csv')");
                    table.CheckConstraint("ck_providers_budget_category", "budget_category IN ('muebles', 'electronica', 'iluminacion', 'decoracion', 'materiales', 'instalacion')");
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    project_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    space_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    length_cm = table.Column<int>(type: "integer", nullable: false),
                    width_cm = table.Column<int>(type: "integer", nullable: false),
                    height_cm = table.Column<int>(type: "integer", nullable: false),
                    measurement_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "medidas_usuario"),
                    surfaces = table.Column<string>(type: "jsonb", nullable: true),
                    commercial = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                    table.CheckConstraint("ck_rooms_dimensions", "length_cm > 0 AND width_cm > 0 AND height_cm > 0");
                    table.CheckConstraint("ck_rooms_measurement_source", "measurement_source IN ('medidas_usuario', 'planos', 'fotografias')");
                    table.CheckConstraint("ck_rooms_space_type", "space_type IN ('sala', 'recamara', 'cocina', 'oficina', 'comedor')");
                    table.ForeignKey(
                        name: "fk_rooms_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "materials",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    price_per_m2_min = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    price_per_m2_max = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    finish = table.Column<string>(type: "text", nullable: true),
                    texture = table.Column<string>(type: "text", nullable: true),
                    provider_id = table.Column<string>(type: "character varying(64)", nullable: true),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_materials", x => x.id);
                    table.CheckConstraint("ck_materials_price", "price_per_m2_min >= 0 AND price_per_m2_max >= price_per_m2_min");
                    table.CheckConstraint("ck_materials_type", "type IN ('madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio')");
                    table.ForeignKey(
                        name: "fk_materials_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    kind = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    width_cm = table.Column<int>(type: "integer", nullable: false),
                    depth_cm = table.Column<int>(type: "integer", nullable: false),
                    height_cm = table.Column<int>(type: "integer", nullable: false),
                    materials = table.Column<string[]>(type: "text[]", nullable: false),
                    colors = table.Column<string[]>(type: "text[]", nullable: false),
                    styles = table.Column<string[]>(type: "text[]", nullable: false),
                    price_min = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    price_max = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    provider_id = table.Column<string>(type: "character varying(64)", nullable: true),
                    sku = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    availability = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    asset_3d = table.Column<string>(type: "text", nullable: true),
                    images = table.Column<string[]>(type: "text[]", nullable: false),
                    variants = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.CheckConstraint("ck_products_availability", "availability IS NULL OR availability IN ('disponible', 'agotado', 'sobre_pedido', 'desconocida')");
                    table.CheckConstraint("ck_products_category", "category IN ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')");
                    table.CheckConstraint("ck_products_colors", "cardinality(colors) > 0");
                    table.CheckConstraint("ck_products_conceptual", "kind = 'real' OR (provider_id IS NULL AND sku IS NULL)");
                    table.CheckConstraint("ck_products_dimensions", "width_cm > 0 AND depth_cm > 0 AND height_cm > 0");
                    table.CheckConstraint("ck_products_kind", "kind IN ('real', 'conceptual')");
                    table.CheckConstraint("ck_products_materials", "cardinality(materials) > 0 AND materials <@ ARRAY['madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio']::text[]");
                    table.CheckConstraint("ck_products_price", "price_min >= 0 AND price_max >= price_min");
                    table.CheckConstraint("ck_products_styles", "cardinality(styles) > 0 AND styles <@ ARRAY['moderno', 'minimalista', 'industrial', 'japandi', 'clasico']::text[]");
                    table.ForeignKey(
                        name: "fk_products_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "designs",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    project_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    room_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<string>(type: "character varying(64)", nullable: true),
                    style = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    budget_mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    favorite = table.Column<bool>(type: "boolean", nullable: false),
                    comments = table.Column<string>(type: "text", nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    render_url = table.Column<string>(type: "text", nullable: true),
                    installation_min = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    installation_max = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    lighting = table.Column<string>(type: "jsonb", nullable: true),
                    structural_confirmations = table.Column<string[]>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_designs", x => x.id);
                    table.CheckConstraint("ck_designs_budget_mode", "budget_mode IN ('minimizar', 'ajustar', 'flexible')");
                    table.CheckConstraint("ck_designs_installation", "(installation_min IS NULL AND installation_max IS NULL) OR (installation_min >= 0 AND installation_max >= installation_min)");
                    table.CheckConstraint("ck_designs_not_own_parent", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_designs_revision", "revision >= 0");
                    table.CheckConstraint("ck_designs_structural_confirmations", "structural_confirmations <@ ARRAY['muro', 'columna', 'puerta', 'ventana']::text[]");
                    table.CheckConstraint("ck_designs_style", "style IN ('moderno', 'minimalista', 'industrial', 'japandi', 'clasico')");
                    table.ForeignKey(
                        name: "fk_designs_designs_parent_id",
                        column: x => x.parent_id,
                        principalTable: "designs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_designs_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_designs_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "requirements",
                columns: table => new
                {
                    room_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    attribute = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    value = table.Column<string>(type: "jsonb", nullable: true),
                    element = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    style = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_requirements", x => new { x.room_id, x.id });
                    table.CheckConstraint("ck_requirements_category", "category IS NULL OR category IN ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')");
                    table.CheckConstraint("ck_requirements_priority", "priority IN ('obligatorio', 'importante', 'preferencia', 'opcional')");
                    table.CheckConstraint("ck_requirements_shape", "(type = 'presupuesto_max' AND amount > 0) OR (type = 'tiene' AND category IS NOT NULL AND attribute IS NOT NULL AND value IS NOT NULL) OR (type = 'conservar' AND element IS NOT NULL) OR (type = 'estilo' AND style IS NOT NULL) OR (type = 'color' AND category IS NOT NULL AND color IS NOT NULL) OR (type = 'categoria' AND category IS NOT NULL)");
                    table.CheckConstraint("ck_requirements_style", "style IS NULL OR style IN ('moderno', 'minimalista', 'industrial', 'japandi', 'clasico')");
                    table.CheckConstraint("ck_requirements_type", "type IN ('presupuesto_max', 'tiene', 'conservar', 'estilo', 'color', 'categoria')");
                    table.ForeignKey(
                        name: "fk_requirements_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_columns",
                columns: table => new
                {
                    room_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    x_cm = table.Column<int>(type: "integer", nullable: false),
                    y_cm = table.Column<int>(type: "integer", nullable: false),
                    width_cm = table.Column<int>(type: "integer", nullable: false),
                    depth_cm = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_columns", x => new { x.room_id, x.id });
                    table.CheckConstraint("ck_room_columns_size", "x_cm >= 0 AND y_cm >= 0 AND width_cm > 0 AND depth_cm > 0");
                    table.ForeignKey(
                        name: "fk_room_columns_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_doors",
                columns: table => new
                {
                    room_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    wall = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position_cm = table.Column<int>(type: "integer", nullable: false),
                    width_cm = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_doors", x => new { x.room_id, x.id });
                    table.CheckConstraint("ck_room_doors_size", "position_cm >= 0 AND width_cm > 0");
                    table.CheckConstraint("ck_room_doors_wall", "wall IN ('norte', 'sur', 'este', 'oeste')");
                    table.ForeignKey(
                        name: "fk_room_doors_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "room_windows",
                columns: table => new
                {
                    room_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    wall = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    position_cm = table.Column<int>(type: "integer", nullable: false),
                    width_cm = table.Column<int>(type: "integer", nullable: false),
                    sill_height_cm = table.Column<int>(type: "integer", nullable: false),
                    height_cm = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_windows", x => new { x.room_id, x.id });
                    table.CheckConstraint("ck_room_windows_size", "position_cm >= 0 AND width_cm > 0 AND sill_height_cm >= 0 AND (height_cm IS NULL OR height_cm > 0)");
                    table.CheckConstraint("ck_room_windows_wall", "wall IN ('norte', 'sur', 'este', 'oeste')");
                    table.ForeignKey(
                        name: "fk_room_windows_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "design_history",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    design_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    author = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    changed_instance_ids = table.Column<string[]>(type: "text[]", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: true),
                    snapshot = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_history", x => x.id);
                    table.CheckConstraint("ck_design_history_revision", "revision IS NULL OR revision >= 0");
                    table.ForeignKey(
                        name: "fk_design_history_designs_design_id",
                        column: x => x.design_id,
                        principalTable: "designs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "design_materials",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    design_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    material_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    surface = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_materials", x => x.id);
                    table.CheckConstraint("ck_design_materials_area", "area_m2 > 0");
                    table.CheckConstraint("ck_design_materials_surface", "surface IS NULL OR surface IN ('piso', 'muros', 'techo')");
                    table.ForeignKey(
                        name: "fk_design_materials_designs_design_id",
                        column: x => x.design_id,
                        principalTable: "designs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_design_materials_materials_material_id",
                        column: x => x.material_id,
                        principalTable: "materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "design_objects",
                columns: table => new
                {
                    design_id = table.Column<string>(type: "character varying(64)", nullable: false),
                    instance_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    catalog_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    x_cm = table.Column<int>(type: "integer", nullable: false),
                    y_cm = table.Column<int>(type: "integer", nullable: false),
                    z_cm = table.Column<int>(type: "integer", nullable: false),
                    rotation = table.Column<int>(type: "integer", nullable: false),
                    scale_x = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.0),
                    scale_y = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.0),
                    scale_z = table.Column<double>(type: "double precision", nullable: false, defaultValue: 1.0),
                    color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    material = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    locked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_design_objects", x => new { x.design_id, x.instance_id });
                    table.CheckConstraint("ck_design_objects_material", "material IS NULL OR material IN ('madera', 'marmol', 'concreto', 'ceramica', 'pintura', 'tela', 'metal', 'vidrio')");
                    table.CheckConstraint("ck_design_objects_rotation", "rotation IN (0, 90, 180, 270)");
                    table.CheckConstraint("ck_design_objects_scale", "scale_x > 0 AND scale_y > 0 AND scale_z > 0");
                    table.CheckConstraint("ck_design_objects_type", "type IN ('sofa', 'sillon', 'mesa_centro', 'mueble_tv', 'cama', 'escritorio', 'silla_oficina', 'librero', 'mesa_comedor', 'silla_comedor', 'isla', 'televisor', 'lampara', 'alfombra', 'cuadro', 'planta')");
                    table.CheckConstraint("ck_design_objects_z", "z_cm >= 0");
                    table.ForeignKey(
                        name: "fk_design_objects_designs_design_id",
                        column: x => x.design_id,
                        principalTable: "designs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_design_objects_products_catalog_id",
                        column: x => x.catalog_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_design_history_design_id_date",
                table: "design_history",
                columns: new[] { "design_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_design_materials_design_id",
                table: "design_materials",
                column: "design_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_materials_material_id",
                table: "design_materials",
                column: "material_id");

            migrationBuilder.CreateIndex(
                name: "ix_design_objects_catalog_id",
                table: "design_objects",
                column: "catalog_id");

            migrationBuilder.CreateIndex(
                name: "ix_designs_parent_id",
                table: "designs",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_designs_project_id",
                table: "designs",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_designs_room_id",
                table: "designs",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ix_materials_provider_id",
                table: "materials",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_products_provider_id",
                table: "products",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_styles",
                table: "products",
                column: "styles")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_project_id",
                table: "rooms",
                column: "project_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "design_history");

            migrationBuilder.DropTable(
                name: "design_materials");

            migrationBuilder.DropTable(
                name: "design_objects");

            migrationBuilder.DropTable(
                name: "requirements");

            migrationBuilder.DropTable(
                name: "room_columns");

            migrationBuilder.DropTable(
                name: "room_doors");

            migrationBuilder.DropTable(
                name: "room_windows");

            migrationBuilder.DropTable(
                name: "materials");

            migrationBuilder.DropTable(
                name: "designs");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "rooms");

            migrationBuilder.DropTable(
                name: "providers");

            migrationBuilder.DropTable(
                name: "projects");
        }
    }
}
