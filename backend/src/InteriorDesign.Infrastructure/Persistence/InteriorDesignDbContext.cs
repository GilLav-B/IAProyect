using InteriorDesign.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InteriorDesign.Infrastructure.Persistence;

public class InteriorDesignDbContext(DbContextOptions<InteriorDesignDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<RoomDoor> RoomDoors => Set<RoomDoor>();
    public DbSet<RoomWindow> RoomWindows => Set<RoomWindow>();
    public DbSet<RoomColumn> RoomColumns => Set<RoomColumn>();
    public DbSet<Requirement> Requirements => Set<Requirement>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<CatalogMaterial> Materials => Set<CatalogMaterial>();
    public DbSet<Design> Designs => Set<Design>();
    public DbSet<DesignObject> DesignObjects => Set<DesignObject>();
    public DbSet<DesignMaterial> DesignMaterials => Set<DesignMaterial>();
    public DbSet<DesignHistoryEntry> DesignHistory => Set<DesignHistoryEntry>();
    public DbSet<AiJob> AiJobs => Set<AiJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InteriorDesignDbContext).Assembly);

    /// <summary>Configuracion comun: tablas y columnas en snake_case.</summary>
    public static void Configure(DbContextOptionsBuilder builder, string connectionString) =>
        builder.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
}
