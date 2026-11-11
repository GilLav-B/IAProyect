using InteriorDesign.Domain.State;
using InteriorDesign.Engine;

namespace InteriorDesign.Api.Services;

/// <summary>Mueble que aparece en una captura del visor y en que parte del cuadro.</summary>
public record VisibleItem(string InstanceId, string Where);

/// <summary>
/// Datos que la API le manda al servicio de IA (Python). Aqui NO se arman prompts: eso lo hace ai-service
/// con las reglas de /ai-rules. La IA solo recibe datos del diseno y del catalogo; nunca claves ni datos personales.
/// </summary>
public static class AiContext
{
    /// <summary>Datos del diseno que necesita la IA para proponer (cuarto, requisitos, objetos, presupuesto, catalogo permitido).</summary>
    public static object DesignContext(DesignState s, DesignEngine engine)
    {
        var allowed = engine.Rules.SpaceCategories.GetValueOrDefault(s.Room.SpaceType) ?? [];
        var budget = engine.Budget(s);
        return new
        {
            space = new
            {
                type = s.Room.SpaceType,
                lengthCm = s.Room.Length,
                widthCm = s.Room.Width,
                heightCm = s.Room.Height,
                doors = s.Room.Doors.Select(d => new { d.Id, d.Wall, positionCm = d.Position, widthCm = d.Width }),
                windows = s.Room.Windows.Select(w => new { w.Id, w.Wall, positionCm = w.Position, widthCm = w.Width, sillHeightCm = w.SillHeight }),
            },
            design = new { s.Design.Name, s.Design.Style, s.Design.BudgetMode },
            requirements = s.Requirements.Select(r => new { r.Priority, requirement = DesignEngine.RequirementTerm(r).ToString() }),
            budget = new { totalMin = budget.Total.Min, totalMax = budget.Total.Max, cap = budget.EffectiveCap },
            objects = s.Objects.Select(o => new
            {
                o.InstanceId,
                o.CatalogId,
                name = engine.Product(o.CatalogId)?.Name,
                category = engine.Product(o.CatalogId)?.Category,
                xCm = o.Position.X,
                yCm = o.Position.Y,
                o.Rotation,
                color = o.Config?.Color,
                o.Locked,
            }),
            allowedCategories = allowed,
            catalog = engine.Catalog.Products
                .Where(p => allowed.Contains(p.Category))
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Category,
                    p.Kind,
                    widthCm = p.Dimensions.Width,
                    depthCm = p.Dimensions.Depth,
                    heightCm = p.Dimensions.Height,
                    p.Colors,
                    p.Styles,
                    priceMin = p.Price.Min,
                    priceMax = p.Price.Max,
                    p.Attributes,
                }),
        };
    }

    /// <summary>Productos del catalogo que usa el diseno (para describir muebles en imagenes y armar Blender).</summary>
    public static List<ProductState> UsedProducts(DesignState s, DesignEngine engine) =>
        s.Objects.Select(o => o.CatalogId).Distinct()
            .Select(engine.Product).Where(p => p is not null).Select(p => p!).ToList();
}
