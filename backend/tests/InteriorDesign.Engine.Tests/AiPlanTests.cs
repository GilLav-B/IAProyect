namespace InteriorDesign.Engine.Tests;

/// <summary>Fase 5: lo que propone la IA siempre pasa por el motor antes de aplicarse.</summary>
public class AiPlanTests
{
    private static readonly DesignEngine Engine = new(States.Catalog());

    [Fact]
    public void Agregar_sin_posicion_usa_un_lugar_libre_valido()
    {
        var (final, outcomes) = AiPlan.Run(States.D4, [new AiProposal("agregar", CatalogId: "planta_001")], Engine);
        Assert.True(outcomes[0].Applied, outcomes[0].Verdict);
        var planta = final.Objects.Single(o => o.CatalogId == "planta_001");
        Assert.Equal("planta_1", planta.InstanceId);
        Assert.True(Engine.IsValid(final));
    }

    [Fact]
    public void Agregar_en_un_lugar_que_choca_se_reubica()
    {
        // (120,60) es donde esta la cama en d4: el motor busca otro lugar para la planta
        var (final, outcomes) = AiPlan.Run(States.D4, [new AiProposal("agregar", CatalogId: "planta_001", X: 130, Y: 70, Rotation: 0)], Engine);
        Assert.True(outcomes[0].Applied);
        var planta = final.Objects.Single(o => o.CatalogId == "planta_001");
        Assert.False(planta.Position.X == 130 && planta.Position.Y == 70);
    }

    [Fact]
    public void Lo_que_el_motor_rechaza_no_se_aplica()
    {
        var d1 = States.D1;
        var (final, outcomes) = AiPlan.Run(d1,
        [
            new AiProposal("quitar", InstanceId: "tv_d1"), // bloqueada y requisito obligatorio
            new AiProposal("agregar", CatalogId: "cama_001"), // no va en una sala
            new AiProposal("agregar", CatalogId: "no_existe"),
            new AiProposal("volar", InstanceId: "sofa_d1"),
            new AiProposal("reemplazar", InstanceId: "lampara_d1", CatalogId: "lampara_001"), // ya es ese producto
        ], Engine);

        Assert.All(outcomes, o => Assert.False(o.Applied));
        Assert.Contains("elemento_bloqueado(tv_d1)", outcomes[0].Verdict);
        Assert.Contains("categoria_no_permitida", outcomes[1].Verdict);
        Assert.Contains("producto_inexistente", outcomes[2].Reasons.Select(r => r.ToString()));
        Assert.Contains("accion_desconocida", outcomes[3].Reasons.Select(r => r.ToString()));
        Assert.Contains("sin_cambio", outcomes[4].Reasons.Select(r => r.ToString()));
        Assert.Equal(d1.Objects.Count, final.Objects.Count);
    }

    [Fact]
    public void Cada_propuesta_se_juzga_con_lo_que_dejaron_las_anteriores()
    {
        // Primero quita la lampara y luego mueve la mesa a donde estaba la lampara
        var (final, outcomes) = AiPlan.Run(States.D1,
        [
            new AiProposal("quitar", InstanceId: "lampara_d1"),
            new AiProposal("mover", InstanceId: "mesa_d1", X: 40, Y: 270, Rotation: 0),
        ], Engine);
        Assert.True(outcomes[0].Applied, outcomes[0].Verdict);
        Assert.True(outcomes[1].Applied, outcomes[1].Verdict);
        Assert.DoesNotContain(final.Objects, o => o.InstanceId == "lampara_d1");
        Assert.Equal(40, final.Objects.Single(o => o.InstanceId == "mesa_d1").Position.X);
        // El original no cambia
        Assert.Contains(States.D1.Objects, o => o.InstanceId == "lampara_d1");
    }
}
