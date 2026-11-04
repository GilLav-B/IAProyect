namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Fase 2: el motor en C# debe dar lo mismo que diseno.pl.
/// Los valores esperados son la salida real de "?- pruebas." en SWI-Prolog 10.0.2.
/// </summary>
public class DesignEngineTests
{
    private static readonly DesignEngine Engine = new(States.Catalog());

    private static string Str(IEnumerable<Term> terms) => Term.L(terms).ToString();

    // ---------- Tests obligatorios del CLAUDE.md ----------

    [Fact]
    public void D1_es_valido_con_advertencia_de_presupuesto()
    {
        var d1 = States.D1;
        Assert.True(Engine.IsValid(d1));
        Assert.Equal("[]", Str(Engine.Conflicts(d1)));
        Assert.Equal("[presupuesto_puede_excederse]", Str(Engine.Warnings(d1)));
    }

    [Fact]
    public void D2_tiene_presupuesto_excedido()
    {
        Assert.Equal("[presupuesto_excedido]", Str(Engine.Conflicts(States.D2)));
    }

    [Fact]
    public void D3_bloquea_la_puerta()
    {
        Assert.Equal("[bloquea_puerta(sofa_d3,p1)]", Str(Engine.Conflicts(States.D3)));
    }

    [Fact]
    public void D4_es_valido()
    {
        Assert.True(Engine.IsValid(States.D4));
    }

    [Fact]
    public void Mover_un_objeto_bloqueado_es_rechazado()
    {
        var v = Engine.Judge(States.D1, new MoveProposal("tv_d1", 300, 100, 0));
        Assert.False(v.Accepted);
        Assert.Equal("rechazada([elemento_bloqueado(tv_d1)])", v.ToString());
    }

    [Fact]
    public void Reemplazar_TV_75_por_65_con_requisito_obligatorio_es_rechazado()
    {
        var v = Engine.Judge(States.D1, new ReplaceProposal("tv_d1", "tv_65"));
        Assert.False(v.Accepted);
        Assert.Contains("rompe_requisito(tiene(televisor,pulgadas,75))", v.ToString());
        Assert.Equal("rechazada([elemento_bloqueado(tv_d1),rompe_requisito(tiene(televisor,pulgadas,75))])", v.ToString());
    }

    [Fact]
    public void Modificar_estructura_sin_confirmacion_es_rechazado()
    {
        var v = Engine.Judge(States.D1, new ModifyStructureProposal("muro"));
        Assert.Equal("rechazada([estructural_protegido(muro)])", v.ToString());
    }

    [Fact]
    public void Modificar_estructura_con_confirmacion_del_usuario_es_aceptado()
    {
        var d1 = States.D1;
        d1.StructuralConfirmations.Add("muro");
        Assert.Equal("aceptada", Engine.Judge(d1, new ModifyStructureProposal("muro")).ToString());
    }

    // ---------- Resto de "?- pruebas." de diseno.pl ----------

    [Fact]
    public void Presupuesto_total_de_d1()
    {
        var total = Engine.BudgetTotal(States.D1);
        Assert.Equal(29299m, total.Min);
        Assert.Equal(39298m, total.Max);
    }

    [Fact]
    public void Advertencias_de_d3()
    {
        Assert.Equal(
            "[presupuesto_puede_excederse,requisito_no_cumplido(preferencia,estilo(moderno)),requisito_no_cumplido(preferencia,color(sofa,beige))]",
            Str(Engine.Warnings(States.D3)));
    }

    [Fact]
    public void Sustitucion_con_ahorro_en_d2()
    {
        var first = Engine.Savings(States.D2).First(x => x.InstanceId == "sofa_d2");
        Assert.Equal("sofa_002", first.Product.Id);
        Assert.Equal(4000m, first.Saving);
    }

    [Fact]
    public void Buscar_similares_de_sofa_001()
    {
        Assert.Equal(["sofa_003", "sofa_002"], Engine.SimilarByPrice("sofa_001").Select(p => p.Id));
    }

    [Theory]
    [InlineData("mover", "sofa_d1,200,110,90", "aceptada")]
    [InlineData("reemplazar", "sofa_d1,sofa_001", "rechazada([fuera_de_habitacion,presupuesto_excedido])")]
    [InlineData("agregar", "alfombra_d1,alfombra_001,150,90,90", "rechazada([presupuesto_excedido])")]
    [InlineData("mover", "lampara_d1,120,345,0", "rechazada([bloquea_ventana(v1)])")]
    [InlineData("agregar", "escr_d1,escritorio_001,100,100,0", "rechazada([categoria_no_permitida,colision_con(sofa_d1),presupuesto_excedido])")]
    public void Veredictos_de_pruebas(string action, string args, string expected)
    {
        var a = args.Split(',');
        Proposal p = action switch
        {
            "mover" => new MoveProposal(a[0], int.Parse(a[1]), int.Parse(a[2]), int.Parse(a[3])),
            "reemplazar" => new ReplaceProposal(a[0], a[1]),
            "agregar" => new AddProposal(a[0], a[1], int.Parse(a[2]), int.Parse(a[3]), int.Parse(a[4])),
            _ => throw new ArgumentException(action)
        };
        Assert.Equal(expected, Engine.Judge(States.D1, p).ToString());
    }

    [Fact]
    public void Comparar_d1_con_d2()
    {
        Assert.Equal("[dif(sofa,sofa_003,sofa_001)]", Str(Engine.Compare(States.D1, States.D2)));
    }

    [Fact]
    public void Accion_sobre_objeto_inexistente_es_accion_invalida()
    {
        Assert.Equal("rechazada([accion_invalida])", Engine.Judge(States.D1, new MoveProposal("no_existe", 0, 0, 0)).ToString());
    }

    [Fact]
    public void Term_imprime_como_writeq()
    {
        Assert.Equal("item('Lampara de piso Arco',1)", Term.C("item", Term.A("Lampara de piso Arco"), Term.N(1)).ToString());
        Assert.Equal("[a,'B',3.5,0.25]", Term.L([Term.A("a"), Term.A("B"), Term.N(3.5m), Term.F(0.25)]).ToString());
    }
}
