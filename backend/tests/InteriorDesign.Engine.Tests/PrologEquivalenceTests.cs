using System.Text.Json;
using InteriorDesign.Domain.State;
using InteriorDesign.PrologBridge;
using Xunit.Abstractions;

namespace InteriorDesign.Engine.Tests;

/// <summary>Solo corre si SWI-Prolog esta instalado.</summary>
public sealed class SwiplFactAttribute : FactAttribute
{
    public SwiplFactAttribute()
    {
        if (PrologRunner.FindSwipl() is null)
            Skip = "SWI-Prolog no esta instalado (define SWIPL_PATH).";
    }
}

/// <summary>
/// Fase 2: compara el motor en C# contra diseno.pl ejecutado en SWI-Prolog, con los ejemplos
/// d1-d4 y disenos sinteticos que fuerzan todos los tipos de conflicto, y miles de propuestas.
/// Cualquier diferencia hace fallar la prueba y se lista.
/// </summary>
public class PrologEquivalenceTests(ITestOutputHelper output)
{
    private static readonly CatalogState Catalog = States.Catalog();
    private static readonly DesignEngine Engine = new(Catalog);

    private static PrologRunner Runner() =>
        new(PrologRunner.FindSwipl()!, PrologRunner.FindRepoRoot(AppContext.BaseDirectory)!, TimeSpan.FromMinutes(5));

    private static string Str(IEnumerable<Term> terms) => Term.L(terms).ToString();

    private static PlacedObject Obj(string inst, string catalogId, int x, int y, int rot = 0, string? color = null, bool locked = false)
    {
        var p = Engine.Product(catalogId);
        return new PlacedObject
        {
            InstanceId = inst, CatalogId = catalogId, Type = p?.Category ?? "sofa",
            Position = new Position { X = x, Y = y }, Rotation = rot, Locked = locked,
            Config = color is null ? null : new ObjectConfig { Color = color }
        };
    }

    private static RequirementState Req(string id, string priority, string type, Action<RequirementState>? fill = null)
    {
        var r = new RequirementState { Id = id, Priority = priority, Type = type };
        fill?.Invoke(r);
        return r;
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

    /// <summary>Disenos sinteticos que cubren todos los conflictos y advertencias.</summary>
    public static List<DesignState> Synthetic()
    {
        // x1: sala con de todo (conceptual, color no disponible, categoria no permitida, puerta, flexible)
        var x1 = States.D1;
        x1.Design.Id = "x1"; x1.Design.ParentId = null; x1.Design.BudgetMode = "flexible"; x1.Design.Style = "japandi";
        x1.Room.Id = "hx1";
        x1.Room.Doors.Add(new DoorState { Id = "p9", Wall = "este", Position = 150, Width = 80 });
        x1.Room.Windows.Add(new WindowState { Id = "v9", Wall = "oeste", Position = 50, Width = 100, SillHeight = 0 });
        x1.Requirements =
        [
            Req("r1", "obligatorio", "presupuesto_max", r => r.Amount = 45000),
            Req("r2", "obligatorio", "tiene", r => { r.Category = "televisor"; r.Attribute = "pulgadas"; r.Value = Json(75); }),
            Req("r3", "importante", "categoria", r => r.Category = "alfombra"),
            Req("r4", "importante", "estilo", r => r.Style = "industrial"),
            Req("r5", "preferencia", "color", r => { r.Category = "sofa"; r.Color = "negro"; }),
            Req("r6", "obligatorio", "conservar", r => r.Element = "piso"),
            Req("r7", "opcional", "categoria", r => r.Category = "planta"),
        ];
        x1.Objects =
        [
            Obj("s1", "sofa_001", 0, 0, 0, "rojo"),
            Obj("tv", "tv_65", 400, 100, 90, locked: true),
            Obj("c1", "concepto_001", 200, 200),
            Obj("e1", "escritorio_001", 300, 250, 90),
            Obj("k1", "cuadro_001", 10, 60),
            Obj("al", "alfombra_001", 100, 50, 90),
            Obj("l1", "lampara_002", 0, 80),
        ];
        x1.Materials = [new AppliedMaterial { MaterialId = "marmol_001", AreaM2 = 12.5m }, new AppliedMaterial { MaterialId = "no_existe", AreaM2 = 3 }];

        // x2: recamara diminuta: no cabe, demasiado alto, fuera de habitacion, falta cama queen
        var x2 = States.D4;
        x2.Design.Id = "x2"; x2.Room.Id = "hx2"; x2.Room.Length = 150; x2.Room.Width = 120; x2.Room.Height = 120;
        x2.Room.Doors = [new DoorState { Id = "pa", Wall = "norte", Position = 10, Width = 70 }];
        x2.Room.Windows = [new WindowState { Id = "va", Wall = "sur", Position = 0, Width = 150, SillHeight = 100 }];
        x2.Objects = [Obj("cama", "cama_001", 0, 0), Obj("lam", "lampara_001", 140, 100), Obj("tv2", "tv_75", 5, 5)];
        x2.Installation = new PriceRange { Min = 500, Max = 800 };

        // x3: oficina saturada: exceso de muebles, colisiones, sin presupuesto_max
        var x3 = States.D4;
        x3.Design.Id = "x3"; x3.Design.Style = "industrial"; x3.Room.Id = "hx3"; x3.Room.SpaceType = "oficina";
        x3.Room.Length = 300; x3.Room.Width = 250;
        x3.Requirements = [Req("q1", "importante", "tiene", r => { r.Category = "escritorio"; r.Attribute = "tamano"; r.Value = Json("grande"); })];
        x3.Objects =
        [
            Obj("es1", "escritorio_001", 0, 100), Obj("es2", "escritorio_001", 130, 100),
            Obj("si1", "silla_oficina_001", 20, 170), Obj("li1", "librero_001", 200, 0, 90),
            Obj("li2", "librero_001", 250, 150), Obj("so", "sofa_002", 0, 0, 0, "gris"),
        ];
        return [x1, x2, x3];
    }

    private static List<DesignState> AllStates() => [States.D1, States.D2, States.D3, States.D4, .. Synthetic()];

    private static IEnumerable<Proposal> ProposalsFor(DesignState s)
    {
        foreach (var o in s.Objects)
        {
            for (var x = -40; x <= s.Room.Length; x += 53)
                for (var y = -30; y <= s.Room.Width; y += 47)
                    foreach (var rot in new[] { 0, 90, 180, 270 })
                        yield return new MoveProposal(o.InstanceId, x, y, rot);
            foreach (var p in Catalog.Products)
                yield return new ReplaceProposal(o.InstanceId, p.Id);
            yield return new ReplaceProposal(o.InstanceId, "no_existe");
            yield return new RemoveProposal(o.InstanceId);
        }
        foreach (var p in Catalog.Products)
            for (var x = 0; x <= s.Room.Length; x += 97)
                for (var y = 0; y <= s.Room.Width; y += 89)
                    foreach (var rot in new[] { 0, 90 })
                        yield return new AddProposal("nuevo", p.Id, x, y, rot);
        if (s.Objects.Count > 0)
            yield return new AddProposal(s.Objects[0].InstanceId, "planta_001", 10, 10, 0);
        yield return new AddProposal("nuevo", "no_existe", 0, 0, 0);
        yield return new MoveProposal("no_existe", 0, 0, 0);
        yield return new RemoveProposal("no_existe");
        foreach (var e in new[] { "muro", "columna", "puerta", "ventana", "sofa" })
            yield return new ModifyStructureProposal(e);
    }

    [SwiplFact]
    public async Task Conflictos_advertencias_y_presupuesto_iguales_a_Prolog()
    {
        var states = AllStates();
        // x1 autoriza modificar puertas para probar confirmacion_usuario
        states.Single(s => s.Design.Id == "x1").StructuralConfirmations.Add("puerta");

        var queries = new Dictionary<string, string>();
        var expected = new Dictionary<string, string>();
        foreach (var s in states)
        {
            var d = s.Design.Id;
            queries[$"c_{d}"] = $"conflictos({d},R)"; expected[$"c_{d}"] = Str(Engine.Conflicts(s));
            queries[$"w_{d}"] = $"advertencias({d},R)"; expected[$"w_{d}"] = Str(Engine.Warnings(s));
            // Prolog suma con flotantes si hay m2 con decimales (45998.0); se normaliza a entero cuando lo es.
            var t = Engine.BudgetTotal(s);
            queries[$"t_{d}"] = $"presupuesto_total({d},Mi,Ma), R = Mi/Ma";
            expected[$"t_{d}"] = $"{Term.N(t.Min)}/{Term.N(t.Max)}";
            queries[$"v_{d}"] = $"(diseno_valido({d}) -> R = si ; R = no)"; expected[$"v_{d}"] = Engine.IsValid(s) ? "si" : "no";
            queries[$"s_{d}"] = $"findall(I-N-A, sustitucion_ahorro({d},I,N,A), R)";
            expected[$"s_{d}"] = "[" + string.Join(",", Engine.Savings(s).Select(x =>
                $"{Term.A(x.InstanceId)}-{Term.A(x.Product.Id)}-{Term.N(x.Saving)}")) + "]";
            foreach (var o in s.Objects)
            {
                queries[$"a_{d}_{o.InstanceId}"] = $"alternativas({d},{Term.A(o.InstanceId)},R)";
                expected[$"a_{d}_{o.InstanceId}"] = Term.L(Engine.Alternatives(s, o.InstanceId).Select(p => Term.A(p.Id))).ToString();
            }
            foreach (var other in states)
            {
                queries[$"cmp_{d}_{other.Design.Id}"] = $"comparar_versiones({d},{other.Design.Id},R)";
                expected[$"cmp_{d}_{other.Design.Id}"] = Str(Engine.Compare(s, other));
            }
        }
        foreach (var p in Catalog.Products)
        {
            queries[$"sim_{p.Id}"] = $"buscar_similares({p.Id},R)";
            expected[$"sim_{p.Id}"] = Term.L(Engine.SimilarByPrice(p.Id).Select(x => Term.A(x.Id))).ToString();
        }

        var actual = await Runner().QueryAsync(states, Catalog, queries);
        AssertSame(expected, actual);
    }

    [SwiplFact]
    public async Task Veredictos_de_miles_de_propuestas_iguales_a_Prolog()
    {
        var states = AllStates();
        states.Single(s => s.Design.Id == "x1").StructuralConfirmations.Add("puerta");

        var queries = new Dictionary<string, string>();
        var expected = new Dictionary<string, string>();
        var n = 0;
        foreach (var s in states)
            foreach (var p in ProposalsFor(s))
            {
                var key = $"p{n++}";
                queries[key] = $"veredicto({s.Design.Id},{p.ToTerm()},R)";
                expected[key] = Engine.Judge(s, p).ToString();
            }

        var actual = await Runner().QueryAsync(states, Catalog, queries);
        output.WriteLine($"Propuestas comparadas: {n}");
        AssertSame(expected, actual, queries);
    }

    private void AssertSame(Dictionary<string, string> expected, Dictionary<string, string> actual,
        Dictionary<string, string>? queries = null)
    {
        // 45998.0 (flotante de Prolog) vale lo mismo que 45998
        static string Normalize(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"(\d+)\.0\b", "$1");
        var diffs = expected
            .Where(e => Normalize(actual[e.Key]) != Normalize(e.Value))
            .Select(e => $"{(queries is null ? e.Key : queries[e.Key])}\n   C#:     {e.Value}\n   Prolog: {actual[e.Key]}")
            .ToList();
        foreach (var d in diffs.Take(30)) output.WriteLine(d);
        Assert.True(diffs.Count == 0, $"{diffs.Count} diferencias entre C# y Prolog:\n{string.Join("\n", diffs.Take(15))}");
    }
}
