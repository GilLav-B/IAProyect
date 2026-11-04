using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine.Tests;

/// <summary>Fase 4: reglas de la habitacion (rangos, puertas y ventanas) y colocacion automatica.</summary>
public class RoomAndPlacementTests
{
    private static readonly DesignEngine Engine = new(States.Catalog());

    [Fact]
    public void Habitaciones_de_ejemplo_son_validas()
    {
        Assert.Empty(RoomRules.Validate(States.D1.Room));
        Assert.Empty(RoomRules.Validate(States.D4.Room));
    }

    [Fact]
    public void Medidas_fuera_de_rango_se_rechazan()
    {
        var r = States.D1.Room;
        r.Length = 100;
        r.Height = 900;
        var errors = RoomRules.Validate(r);
        Assert.Contains(errors, e => e.Contains("largo"));
        Assert.Contains(errors, e => e.Contains("alto"));
    }

    [Fact]
    public void Puerta_que_se_sale_de_la_pared_o_se_encima_se_rechaza()
    {
        var r = States.D1.Room;
        r.Doors.Add(new DoorState { Id = "p2", Wall = "sur", Position = 480, Width = 90 }); // 480+90 > 520
        r.Windows.Add(new WindowState { Id = "v2", Wall = "norte", Position = 200, Width = 100, SillHeight = 90 }); // encima de v1 (100-250)
        var errors = RoomRules.Validate(r);
        Assert.Contains(errors, e => e.Contains("p2") && e.Contains("se sale"));
        Assert.Contains(errors, e => e.Contains("v2") && e.Contains("encima"));
    }

    [Fact]
    public void Sugerir_lugar_encuentra_una_posicion_valida()
    {
        var d4 = States.D4;
        var (placement, verdict) = Engine.SuggestPlacement(d4, "planta_1", "planta_001");
        Assert.NotNull(placement);
        Assert.True(verdict.Accepted);
        // La posicion sugerida tambien la acepta el veredicto normal
        Assert.Equal("aceptada", Engine.Judge(d4, placement!).ToString());
    }

    [Fact]
    public void Sugerir_lugar_no_coloca_si_rebasa_el_presupuesto()
    {
        var (placement, verdict) = Engine.SuggestPlacement(States.D1, "alfombra_1", "alfombra_001");
        Assert.Null(placement);
        Assert.Equal("rechazada([presupuesto_excedido])", verdict.ToString());
    }

    [Fact]
    public void Sugerir_lugar_respeta_categorias_del_espacio()
    {
        var (placement, verdict) = Engine.SuggestPlacement(States.D4, "isla_1", "isla_001");
        Assert.Null(placement);
        Assert.Contains("categoria_no_permitida", verdict.ToString());
    }
}
