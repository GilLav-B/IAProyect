using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Rotacion libre (0-359). Los multiplos de 90 siguen siendo rectangulos exactos (como diseno.pl);
/// los demas angulos usan rectangulos girados y el teorema del eje separador.
/// </summary>
public class FreeRotationTests
{
    private static readonly DesignEngine Engine = new(States.Catalog());
    private static readonly Dimensions Square = new() { Width = 100, Depth = 100, Height = 50 };

    [Fact]
    public void Rotaciones_de_90_siguen_siendo_rectangulos_exactos()
    {
        var f = Geometry.Footprint(new Dimensions { Width = 160, Depth = 85, Height = 80 }, 180, 110, 90);
        Assert.True(f.AxisAligned);
        Assert.Equal(new Rect(180, 110, 265, 270), f.Bounds);
    }

    [Fact]
    public void A_45_grados_la_caja_envolvente_crece()
    {
        var f = Geometry.Footprint(Square, 0, 0, 45);
        Assert.False(f.AxisAligned);
        // 100 * cos45 + 100 * sin45 = 141.42 -> 142
        Assert.Equal(new Rect(0, 0, 142, 142), f.Bounds);
        Assert.Equal(10000, f.Area, 3);
    }

    [Fact]
    public void Rombo_no_choca_con_lo_que_solo_toca_su_caja_envolvente()
    {
        var diamond = Geometry.Footprint(Square, 0, 0, 45);
        // La esquina de la caja envolvente queda fuera del rombo
        Assert.False(diamond.Overlaps(new Rect(0, 0, 15, 15)));
        // El centro si choca
        Assert.True(diamond.Overlaps(new Rect(60, 60, 80, 80)));
    }

    [Fact]
    public void Dos_objetos_girados_chocan_solo_si_se_enciman()
    {
        var a = Geometry.Footprint(Square, 0, 0, 30);
        var lejos = Geometry.Footprint(Square, 200, 0, 30);
        var encima = Geometry.Footprint(Square, 50, 20, 60);
        Assert.False(a.Overlaps(lejos));
        Assert.True(a.Overlaps(encima));
    }

    [Fact]
    public void Girar_libre_un_mueble_se_valida_contra_puertas_paredes_y_muebles()
    {
        var d1 = States.D1;
        // Lampara a 30 grados en un rincon libre: aceptada
        Assert.Equal("aceptada", Engine.Judge(d1, new MoveProposal("lampara_d1", 60, 300, 30)).ToString());
        // Sofa a 45 grados pegado a la pared sur: su caja se sale del cuarto
        Assert.Contains("fuera_de_habitacion", Engine.Judge(d1, new MoveProposal("sofa_d1", 200, -5, 45)).ToString());
        // Sofa girado encima de la mesa de centro
        Assert.Contains("colision_con(mesa_d1)", Engine.Judge(d1, new MoveProposal("sofa_d1", 300, 120, 20)).ToString());
    }

    [Fact]
    public void Los_conflictos_del_diseno_funcionan_con_angulos_libres()
    {
        var d1 = States.D1;
        // Girado 15 grados el sofa ocupa mas a lo ancho y alcanza la mesa de centro
        d1.Objects.Single(o => o.InstanceId == "sofa_d1").Rotation = 15;
        Assert.Contains("colision(mesa_d1,sofa_d1)", Term.L(Engine.Conflicts(d1)).ToString());

        // Moviendo la mesa ya no hay conflictos
        d1.Objects.Single(o => o.InstanceId == "mesa_d1").Position = new Position { X = 400, Y = 250 };
        Assert.True(Engine.IsValid(d1), Term.L(Engine.Conflicts(d1)).ToString());
    }
}
