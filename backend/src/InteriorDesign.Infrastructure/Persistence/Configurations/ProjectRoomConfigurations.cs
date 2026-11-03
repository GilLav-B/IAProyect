using InteriorDesign.Domain;
using InteriorDesign.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InteriorDesign.Infrastructure.Persistence.Configurations;

internal class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("projects", t => t.HasCheckConstraint("ck_projects_type", Check.In("type", DomainValues.ProjectTypes)));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Type).HasMaxLength(20);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
    }
}

internal class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> b)
    {
        b.ToTable("rooms", t =>
        {
            t.HasCheckConstraint("ck_rooms_space_type", Check.In("space_type", DomainValues.SpaceTypes));
            t.HasCheckConstraint("ck_rooms_measurement_source", Check.In("measurement_source", DomainValues.MeasurementSources));
            t.HasCheckConstraint("ck_rooms_dimensions", "length_cm > 0 AND width_cm > 0 AND height_cm > 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.SpaceType).HasMaxLength(20);
        b.Property(x => x.MeasurementSource).HasMaxLength(20).HasDefaultValue("medidas_usuario");
        b.Property(x => x.SurfacesJson).HasColumnName("surfaces").HasColumnType("jsonb");
        b.Property(x => x.CommercialJson).HasColumnName("commercial").HasColumnType("jsonb");

        b.HasOne(x => x.Project).WithMany(p => p.Rooms).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Doors).WithOne().HasForeignKey(d => d.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Windows).WithOne().HasForeignKey(w => w.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Columns).WithOne().HasForeignKey(c => c.RoomId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Requirements).WithOne().HasForeignKey(r => r.RoomId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class RoomDoorConfiguration : IEntityTypeConfiguration<RoomDoor>
{
    public void Configure(EntityTypeBuilder<RoomDoor> b)
    {
        b.ToTable("room_doors", t =>
        {
            t.HasCheckConstraint("ck_room_doors_wall", Check.In("wall", DomainValues.Walls));
            t.HasCheckConstraint("ck_room_doors_size", "position_cm >= 0 AND width_cm > 0");
        });
        b.HasKey(x => new { x.RoomId, x.Id });
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Wall).HasMaxLength(10);
    }
}

internal class RoomWindowConfiguration : IEntityTypeConfiguration<RoomWindow>
{
    public void Configure(EntityTypeBuilder<RoomWindow> b)
    {
        b.ToTable("room_windows", t =>
        {
            t.HasCheckConstraint("ck_room_windows_wall", Check.In("wall", DomainValues.Walls));
            t.HasCheckConstraint("ck_room_windows_size",
                "position_cm >= 0 AND width_cm > 0 AND sill_height_cm >= 0 AND (height_cm IS NULL OR height_cm > 0)");
        });
        b.HasKey(x => new { x.RoomId, x.Id });
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Wall).HasMaxLength(10);
    }
}

internal class RoomColumnConfiguration : IEntityTypeConfiguration<RoomColumn>
{
    public void Configure(EntityTypeBuilder<RoomColumn> b)
    {
        b.ToTable("room_columns", t =>
            t.HasCheckConstraint("ck_room_columns_size", "x_cm >= 0 AND y_cm >= 0 AND width_cm > 0 AND depth_cm > 0"));
        b.HasKey(x => new { x.RoomId, x.Id });
        b.Property(x => x.Id).HasMaxLength(64);
    }
}

internal class RequirementConfiguration : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> b)
    {
        b.ToTable("requirements", t =>
        {
            t.HasCheckConstraint("ck_requirements_priority", Check.In("priority", DomainValues.Priorities));
            t.HasCheckConstraint("ck_requirements_type", Check.In("type", DomainValues.RequirementTypes));
            t.HasCheckConstraint("ck_requirements_category", Check.NullOrIn("category", DomainValues.Categories));
            t.HasCheckConstraint("ck_requirements_style", Check.NullOrIn("style", DomainValues.Styles));
            // Campos obligatorios segun el tipo (igual que el oneOf de design-state.schema.json)
            t.HasCheckConstraint("ck_requirements_shape",
                "(type = 'presupuesto_max' AND amount > 0) OR " +
                "(type = 'tiene' AND category IS NOT NULL AND attribute IS NOT NULL AND value IS NOT NULL) OR " +
                "(type = 'conservar' AND element IS NOT NULL) OR " +
                "(type = 'estilo' AND style IS NOT NULL) OR " +
                "(type = 'color' AND category IS NOT NULL AND color IS NOT NULL) OR " +
                "(type = 'categoria' AND category IS NOT NULL)");
        });
        b.HasKey(x => new { x.RoomId, x.Id });
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Priority).HasMaxLength(20);
        b.Property(x => x.Type).HasMaxLength(20);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.Category).HasMaxLength(30);
        b.Property(x => x.Attribute).HasMaxLength(50);
        b.Property(x => x.ValueJson).HasColumnName("value").HasColumnType("jsonb");
        b.Property(x => x.Element).HasMaxLength(50);
        b.Property(x => x.Style).HasMaxLength(20);
        b.Property(x => x.Color).HasMaxLength(50);
    }
}
