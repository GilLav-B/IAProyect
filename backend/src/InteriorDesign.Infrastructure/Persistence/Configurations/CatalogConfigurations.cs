using InteriorDesign.Domain;
using InteriorDesign.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InteriorDesign.Infrastructure.Persistence.Configurations;

internal class ProviderConfiguration : IEntityTypeConfiguration<Provider>
{
    public void Configure(EntityTypeBuilder<Provider> b)
    {
        b.ToTable("providers", t =>
        {
            t.HasCheckConstraint("ck_providers_budget_category", Check.In("budget_category", DomainValues.BudgetCategories));
            t.HasCheckConstraint("ck_providers_adapter", Check.In("adapter", DomainValues.Adapters));
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.BudgetCategory).HasMaxLength(20);
        b.Property(x => x.Adapter).HasMaxLength(10);
    }
}

internal class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_category", Check.In("category", DomainValues.Categories));
            t.HasCheckConstraint("ck_products_kind", Check.In("kind", DomainValues.ProductKinds));
            t.HasCheckConstraint("ck_products_dimensions", "width_cm > 0 AND depth_cm > 0 AND height_cm > 0");
            t.HasCheckConstraint("ck_products_price", "price_min >= 0 AND price_max >= price_min");
            t.HasCheckConstraint("ck_products_materials",
                $"cardinality(materials) > 0 AND {Check.ArrayIn("materials", DomainValues.MaterialTypes)}");
            t.HasCheckConstraint("ck_products_styles",
                $"cardinality(styles) > 0 AND {Check.ArrayIn("styles", DomainValues.Styles)}");
            t.HasCheckConstraint("ck_products_colors", "cardinality(colors) > 0");
            t.HasCheckConstraint("ck_products_availability", Check.NullOrIn("availability", DomainValues.Availabilities));
            // Un elemento conceptual no se puede comprar: sin proveedor ni SKU
            t.HasCheckConstraint("ck_products_conceptual",
                "kind = 'real' OR (provider_id IS NULL AND sku IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Category).HasMaxLength(30);
        b.Property(x => x.Kind).HasMaxLength(12);
        b.Property(x => x.PriceMin).HasPrecision(12, 2);
        b.Property(x => x.PriceMax).HasPrecision(12, 2);
        b.Property(x => x.AttributesJson).HasColumnName("attributes").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        b.Property(x => x.Sku).HasMaxLength(100);
        b.Property(x => x.Availability).HasMaxLength(20);
        b.Property(x => x.Asset3D).HasColumnName("asset_3d");

        b.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.Category);
        b.HasIndex(x => x.Styles).HasMethod("gin");
    }
}

internal class CatalogMaterialConfiguration : IEntityTypeConfiguration<CatalogMaterial>
{
    public void Configure(EntityTypeBuilder<CatalogMaterial> b)
    {
        b.ToTable("materials", t =>
        {
            t.HasCheckConstraint("ck_materials_type", Check.In("type", DomainValues.MaterialTypes));
            t.HasCheckConstraint("ck_materials_price", "price_per_m2_min >= 0 AND price_per_m2_max >= price_per_m2_min");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Type).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.PricePerM2Min).HasColumnName("price_per_m2_min").HasPrecision(12, 2);
        b.Property(x => x.PricePerM2Max).HasColumnName("price_per_m2_max").HasPrecision(12, 2);
        b.Property(x => x.Color).HasMaxLength(50);
        b.Property(x => x.Sku).HasMaxLength(100);

        b.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.Restrict);
    }
}
