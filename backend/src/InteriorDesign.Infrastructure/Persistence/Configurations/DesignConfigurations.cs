using InteriorDesign.Domain;
using InteriorDesign.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InteriorDesign.Infrastructure.Persistence.Configurations;

internal class DesignConfiguration : IEntityTypeConfiguration<Design>
{
    public void Configure(EntityTypeBuilder<Design> b)
    {
        b.ToTable("designs", t =>
        {
            t.HasCheckConstraint("ck_designs_style", Check.In("style", DomainValues.Styles));
            t.HasCheckConstraint("ck_designs_budget_mode", Check.In("budget_mode", DomainValues.BudgetModes));
            t.HasCheckConstraint("ck_designs_revision", "revision >= 0");
            t.HasCheckConstraint("ck_designs_installation",
                "(installation_min IS NULL AND installation_max IS NULL) OR " +
                "(installation_min >= 0 AND installation_max >= installation_min)");
            t.HasCheckConstraint("ck_designs_structural_confirmations",
                Check.ArrayIn("structural_confirmations", DomainValues.StructuralElements));
            t.HasCheckConstraint("ck_designs_not_own_parent", "parent_id IS NULL OR parent_id <> id");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Style).HasMaxLength(20);
        b.Property(x => x.BudgetMode).HasMaxLength(20);
        b.Property(x => x.Author).HasMaxLength(200);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.InstallationMin).HasPrecision(12, 2);
        b.Property(x => x.InstallationMax).HasPrecision(12, 2);
        b.Property(x => x.LightingJson).HasColumnName("lighting").HasColumnType("jsonb");

        b.HasOne(x => x.Project).WithMany(p => p.Designs).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        // No se puede borrar una habitacion que tiene versiones
        b.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
        // Borrar una version no borra sus hijas (versiones independientes)
        b.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.SetNull);
        b.HasMany(x => x.Objects).WithOne(o => o.Design).HasForeignKey(o => o.DesignId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Materials).WithOne(m => m.Design).HasForeignKey(m => m.DesignId).OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.History).WithOne(h => h.Design).HasForeignKey(h => h.DesignId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class DesignObjectConfiguration : IEntityTypeConfiguration<DesignObject>
{
    public void Configure(EntityTypeBuilder<DesignObject> b)
    {
        b.ToTable("design_objects", t =>
        {
            t.HasCheckConstraint("ck_design_objects_type", Check.In("type", DomainValues.Categories));
            t.HasCheckConstraint("ck_design_objects_rotation", $"rotation >= 0 AND rotation <= {DomainValues.MaxRotation}");
            t.HasCheckConstraint("ck_design_objects_z", "z_cm >= 0");
            t.HasCheckConstraint("ck_design_objects_scale", "scale_x > 0 AND scale_y > 0 AND scale_z > 0");
            t.HasCheckConstraint("ck_design_objects_material", Check.NullOrIn("material", DomainValues.MaterialTypes));
        });
        b.HasKey(x => new { x.DesignId, x.InstanceId });
        b.Property(x => x.InstanceId).HasMaxLength(64);
        b.Property(x => x.CatalogId).HasMaxLength(64);
        b.Property(x => x.Type).HasMaxLength(30);
        b.Property(x => x.ScaleX).HasDefaultValue(1.0);
        b.Property(x => x.ScaleY).HasDefaultValue(1.0);
        b.Property(x => x.ScaleZ).HasDefaultValue(1.0);
        b.Property(x => x.Color).HasMaxLength(50);
        b.Property(x => x.Material).HasMaxLength(20);

        // No se puede borrar un producto que esta usado en algun diseno
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.CatalogId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class DesignMaterialConfiguration : IEntityTypeConfiguration<DesignMaterial>
{
    public void Configure(EntityTypeBuilder<DesignMaterial> b)
    {
        b.ToTable("design_materials", t =>
        {
            t.HasCheckConstraint("ck_design_materials_area", "area_m2 > 0");
            t.HasCheckConstraint("ck_design_materials_surface", Check.NullOrIn("surface", DomainValues.Surfaces));
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();
        b.Property(x => x.AreaM2).HasColumnName("area_m2").HasPrecision(10, 2);
        b.Property(x => x.Surface).HasMaxLength(10);

        b.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal class DesignHistoryEntryConfiguration : IEntityTypeConfiguration<DesignHistoryEntry>
{
    public void Configure(EntityTypeBuilder<DesignHistoryEntry> b)
    {
        b.ToTable("design_history", t => t.HasCheckConstraint("ck_design_history_revision", "revision IS NULL OR revision >= 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).UseIdentityAlwaysColumn();
        b.Property(x => x.Date).HasDefaultValueSql("now()");
        b.Property(x => x.Author).HasMaxLength(200);
        b.Property(x => x.SnapshotJson).HasColumnName("snapshot").HasColumnType("jsonb");
        b.HasIndex(x => new { x.DesignId, x.Date });
    }
}
