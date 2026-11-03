using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InteriorDesign.Infrastructure.Persistence;

/// <summary>
/// Lo usa "dotnet ef" para generar/aplicar migraciones.
/// Toma la conexion de las variables POSTGRES_* (ver .env.example).
/// Generar migraciones no se conecta a la BD; aplicarlas (database update) si.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InteriorDesignDbContext>
{
    public InteriorDesignDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<InteriorDesignDbContext>();
        InteriorDesignDbContext.Configure(builder, ConnectionStrings.FromEnvironment());
        return new InteriorDesignDbContext(builder.Options);
    }
}
