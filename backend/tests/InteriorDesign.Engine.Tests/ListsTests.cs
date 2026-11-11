namespace InteriorDesign.Engine.Tests;

/// <summary>Fase 7: listas de compras y de materiales (seccion 19 de diseno.pl).</summary>
public class ListsTests
{
    private static readonly DesignEngine Engine = new(States.Catalog());

    [Fact]
    public void Lista_de_compras_agrupa_por_producto_con_su_rubro_y_precio()
    {
        var items = Engine.ShoppingList(States.D1);
        Assert.Equal(States.D1.Objects.Count, items.Sum(i => i.Quantity));
        Assert.Equal(items.Select(i => i.CatalogId).Order(StringComparer.Ordinal), items.Select(i => i.CatalogId));
        var tv = items.Single(i => i.CatalogId == "tv_75");
        Assert.Equal("electronica", tv.Rubro);
        Assert.Equal(tv.Unit.Min * tv.Quantity, tv.Total.Min);
    }

    [Fact]
    public void Lista_de_materiales_suma_10_por_ciento_y_redondea_hacia_arriba()
    {
        // d1 aplica 40 m2 de pintura_001: 40 * 1.10 = 44
        var item = Assert.Single(Engine.MaterialList(States.D1));
        Assert.Equal("pintura_001", item.MaterialId);
        Assert.Equal(40, item.AreaM2);
        Assert.Equal(44, item.AreaWithWaste);
        Assert.Equal(60 * 44, item.Total.Min);
        Assert.Equal(100 * 44, item.Total.Max);
        Assert.Equal("item('Pintura vinilica blanca',44,m2)", item.ToTerm().ToString());
    }

    [Fact]
    public void Diseno_sin_materiales_da_lista_vacia()
    {
        var s = States.D4;
        s.Materials.Clear();
        Assert.Empty(Engine.MaterialList(s));
    }
}
