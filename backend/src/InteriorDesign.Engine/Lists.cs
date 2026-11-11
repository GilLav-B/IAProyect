using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>Renglon de lista_compras/2: un producto, cuantas piezas y su rubro.</summary>
public sealed record ShoppingItem(string Rubro, string CatalogId, string Name, string Kind, int Quantity, PriceRange Unit, PriceRange Total)
{
    public Term ToTerm() => Term.C("item", Term.A(Rubro), Term.N(Quantity), Term.A(Name));
}

/// <summary>Renglon de lista_materiales/2: m2 del diseno y m2 a comprar con el factor de desperdicio.</summary>
public sealed record MaterialItem(string MaterialId, string Name, string Type, decimal AreaM2, decimal AreaWithWaste, PriceRange PricePerM2, PriceRange Total)
{
    public Term ToTerm() => Term.C("item", Term.A(Name), Term.N(AreaWithWaste), Term.A("m2"));
}

/// <summary>Seccion 19 de diseno.pl: lista de compras y de materiales.</summary>
public static class Lists
{
    /// <summary>
    /// lista_compras/2: un renglon por producto distinto (ordenado por id, como sort/2 de Prolog) con su cantidad.
    /// Ademas de lo que da Prolog (rubro, cantidad, nombre) lleva precio unitario y subtotal para el reporte.
    /// </summary>
    public static List<ShoppingItem> ShoppingList(this DesignEngine engine, DesignState s) =>
        s.Objects
            .GroupBy(o => o.CatalogId)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => (Product: engine.Product(g.Key), Count: g.Count()))
            .Where(x => x.Product is not null && engine.Rules.CategoryBudget.ContainsKey(x.Product.Category))
            .Select(x => new ShoppingItem(
                engine.Rules.CategoryBudget[x.Product!.Category], x.Product.Id, x.Product.Name, x.Product.Kind, x.Count,
                x.Product.Price,
                new PriceRange { Min = x.Product.Price.Min * x.Count, Max = x.Product.Price.Max * x.Count }))
            .ToList();

    /// <summary>
    /// lista_materiales/2: m2 a comprar = techo(m2 * (1 + desperdicio)). El subtotal usa esos m2 (lo que se compra);
    /// el presupuesto del motor sigue usando los m2 del diseno, igual que presupuesto_rubro/4.
    /// </summary>
    public static List<MaterialItem> MaterialList(this DesignEngine engine, DesignState s) =>
        s.Materials
            .Select(m => (Applied: m, Material: engine.Catalog.Materials.FirstOrDefault(x => x.Id == m.MaterialId)))
            .Where(x => x.Material is not null)
            .Select(x =>
            {
                var buy = Math.Ceiling(x.Applied.AreaM2 * (1 + engine.Rules.MaterialWaste));
                return new MaterialItem(x.Material!.Id, x.Material.Name, x.Material.Type, x.Applied.AreaM2, buy, x.Material.PricePerM2,
                    new PriceRange { Min = x.Material.PricePerM2.Min * buy, Max = x.Material.PricePerM2.Max * buy });
            })
            .ToList();
}
