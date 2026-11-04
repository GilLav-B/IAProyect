using System.Text.Json;
using InteriorDesign.Domain.State;

namespace InteriorDesign.Engine;

/// <summary>
/// Design Engine: port a C# de las reglas de diseno.pl. Es la validacion definitiva:
/// la IA propone, el motor valida, el sistema acepta o rechaza.
/// Cada metodo indica el predicado de Prolog que reproduce; el orden de los resultados
/// es el mismo que da findall/3 en Prolog.
/// </summary>
public class DesignEngine(CatalogState catalog, Rules? rules = null)
{
    private readonly Rules _rules = rules ?? Rules.Default;
    private readonly Dictionary<string, ProductState> _products = catalog.Products.ToDictionary(p => p.Id);
    private readonly Dictionary<string, MaterialState> _materials = catalog.Materials.ToDictionary(m => m.Id);

    public Rules Rules => _rules;
    public CatalogState Catalog => catalog;

    public ProductState? Product(string id) => _products.GetValueOrDefault(id);

    // ---------------------------------------------------------------- utilidades

    /// <summary>Objetos cuyo producto existe en el catalogo, con su huella (huella/3).</summary>
    private IEnumerable<(PlacedObject Obj, ProductState Product, Rect Rect)> Placed(DesignState s) =>
        from o in s.Objects
        let p = Product(o.CatalogId)
        where p is not null
        select (o, p, Geometry.Footprint(p.Dimensions, o.Position.X, o.Position.Y, o.Rotation));

    private bool OccupiesFloor(ProductState p) => !_rules.NoFloorArea.Contains(p.Category);

    private static bool AttributeEquals(ProductState? p, string key, JsonElement? value)
    {
        if (p is null || value is null || !p.Attributes.TryGetValue(key, out var actual))
            return false;
        var expected = value.Value;
        return (actual.ValueKind, expected.ValueKind) switch
        {
            (JsonValueKind.Number, JsonValueKind.Number) => actual.GetDecimal() == expected.GetDecimal(),
            (JsonValueKind.String, JsonValueKind.String) => actual.GetString() == expected.GetString(),
            _ => false
        };
    }

    private static Term ValueTerm(JsonElement? value) => value?.ValueKind switch
    {
        JsonValueKind.Number => Term.N(value.Value.GetDecimal()),
        JsonValueKind.String => Term.A(value.Value.GetString()!),
        _ => Term.A("desconocido")
    };

    /// <summary>Requisito como termino de Prolog, ej. tiene(televisor,pulgadas,75).</summary>
    public static Term RequirementTerm(RequirementState r) => r.Type switch
    {
        "presupuesto_max" => Term.C("presupuesto_max", Term.N(r.Amount ?? 0)),
        "tiene" => Term.C("tiene", Term.A(r.Category!), Term.A(r.Attribute!), ValueTerm(r.Value)),
        "conservar" => Term.C("conservar", Term.A(r.Element!)),
        "estilo" => Term.C("estilo", Term.A(r.Style!)),
        "color" => Term.C("color", Term.A(r.Category!), Term.A(r.Color!)),
        "categoria" => Term.C("categoria", Term.A(r.Category!)),
        _ => Term.A(r.Type)
    };

    // ---------------------------------------------------------------- presupuesto

    /// <summary>presupuesto_rubro/4 para cada rubro, en el orden de diseno.pl.</summary>
    public Dictionary<string, PriceRange> BudgetByCategory(DesignState s)
    {
        var result = new Dictionary<string, PriceRange>();
        foreach (var rubro in _rules.BudgetCategories)
        {
            result[rubro] = rubro switch
            {
                "materiales" => s.Materials
                    .Select(m => (m, mat: _materials.GetValueOrDefault(m.MaterialId)))
                    .Where(x => x.mat is not null)
                    .Aggregate(PriceRange.Zero, (acc, x) => acc + new PriceRange
                    {
                        Min = x.mat!.PricePerM2.Min * x.m.AreaM2,
                        Max = x.mat.PricePerM2.Max * x.m.AreaM2
                    }),
                "instalacion" => s.Installation is { } i ? new PriceRange { Min = i.Min, Max = i.Max } : PriceRange.Zero,
                _ => s.Objects
                    .Select(o => Product(o.CatalogId))
                    .Where(p => p is not null && _rules.CategoryBudget.GetValueOrDefault(p.Category) == rubro)
                    .Aggregate(PriceRange.Zero, (acc, p) => acc + p!.Price)
            };
        }
        return result;
    }

    /// <summary>presupuesto_total/3</summary>
    public PriceRange BudgetTotal(DesignState s) =>
        BudgetByCategory(s).Values.Aggregate(PriceRange.Zero, (a, b) => a + b);

    /// <summary>presupuesto_max/2: el primer requisito presupuesto_max de la habitacion (cualquier prioridad).</summary>
    public static decimal? BudgetMax(DesignState s) =>
        s.Requirements.FirstOrDefault(r => r.Type == "presupuesto_max")?.Amount;

    /// <summary>tope_efectivo/2: en modo flexible se permite el margen extra.</summary>
    public decimal? EffectiveCap(DesignState s) =>
        BudgetMax(s) is not { } max ? null
        : s.Design.BudgetMode == "flexible" ? max * (1 + _rules.FlexibleMargin)
        : max;

    public BudgetSnapshot Budget(DesignState s) => new()
    {
        ByCategory = BudgetByCategory(s),
        Total = BudgetTotal(s),
        EffectiveCap = EffectiveCap(s)
    };

    // ---------------------------------------------------------------- requisitos

    /// <summary>cumple_requisito/2</summary>
    public bool Meets(DesignState s, RequirementState r) => r.Type switch
    {
        "tiene" => s.Objects.Any(o => Product(o.CatalogId) is { } p && p.Category == r.Category
                                      && AttributeEquals(p, r.Attribute!, r.Value)),
        "estilo" => s.Design.Style == r.Style,
        "color" => s.Objects.Any(o => Product(o.CatalogId) is { } p && p.Category == r.Category
                                      && o.Config?.Color == r.Color),
        "categoria" => s.Objects.Any(o => Product(o.CatalogId) is { } p && p.Category == r.Category),
        _ => false
    };

    // ---------------------------------------------------------------- conflictos

    /// <summary>conflictos/2: lo que impide aceptar el diseno.</summary>
    public List<Term> Conflicts(DesignState s)
    {
        var room = s.Room;
        var placed = Placed(s).ToList();
        var result = new List<Term>();

        foreach (var (o, p, _) in placed)
        {
            int w = p.Dimensions.Width, d = p.Dimensions.Depth;
            if (!((w <= room.Length && d <= room.Width) || (d <= room.Length && w <= room.Width)))
                result.Add(Term.C("no_cabe", Term.A(o.InstanceId)));
        }
        foreach (var (o, p, _) in placed)
            if (p.Dimensions.Height > room.Height)
                result.Add(Term.C("demasiado_alto", Term.A(o.InstanceId)));
        foreach (var (o, _, r) in placed)
            if (!Geometry.Inside(room, r))
                result.Add(Term.C("fuera_de_habitacion", Term.A(o.InstanceId)));
        foreach (var a in placed)
            foreach (var b in placed)
                if (string.CompareOrdinal(a.Obj.InstanceId, b.Obj.InstanceId) < 0
                    && OccupiesFloor(a.Product) && OccupiesFloor(b.Product) && a.Rect.Overlaps(b.Rect))
                    result.Add(Term.C("colision", Term.A(a.Obj.InstanceId), Term.A(b.Obj.InstanceId)));
        foreach (var (o, p, r) in placed)
            if (OccupiesFloor(p))
                foreach (var door in room.Doors)
                    if (r.Overlaps(Geometry.DoorZone(room, door)))
                        result.Add(Term.C("bloquea_puerta", Term.A(o.InstanceId), Term.A(door.Id)));
        foreach (var (o, p, r) in placed)
            if (OccupiesFloor(p))
                foreach (var window in room.Windows)
                    if (p.Dimensions.Height > window.SillHeight
                        && r.Overlaps(Geometry.WindowZone(room, window, _rules.WindowAccessCm)))
                        result.Add(Term.C("bloquea_ventana", Term.A(o.InstanceId), Term.A(window.Id)));

        var occupied = placed.Where(x => OccupiesFloor(x.Product)).Sum(x => x.Rect.Area);
        var floor = room.Length * room.Width;
        if (floor > 0 && (decimal)occupied / floor > _rules.MaxOccupancy)
            result.Add(Term.C("exceso_muebles", occupied % floor == 0
                ? Term.N(occupied / floor)
                : Term.F((double)occupied / floor)));

        foreach (var (o, p, _) in placed)
            if (!_rules.CategoryAllowed(room.SpaceType, p.Category))
                result.Add(Term.C("categoria_no_permitida", Term.A(o.InstanceId)));
        foreach (var (o, p, _) in placed)
            if (o.Config?.Color is { } color && !p.Colors.Contains(color))
                result.Add(Term.C("color_no_disponible", Term.A(o.InstanceId), Term.A(color)));

        if (EffectiveCap(s) is { } cap && BudgetTotal(s).Min > cap)
            result.Add(Term.A("presupuesto_excedido"));

        foreach (var r in s.Requirements)
            if (r.Priority == "obligatorio" && _rules.EvaluableRequirements.Contains(r.Type) && !Meets(s, r))
                result.Add(Term.C("falta_requisito", RequirementTerm(r)));

        return result;
    }

    // ---------------------------------------------------------------- advertencias

    /// <summary>advertencias/2: no bloquean, pero se informan.</summary>
    public List<Term> Warnings(DesignState s)
    {
        var result = new List<Term>();

        if (EffectiveCap(s) is { } cap)
        {
            var total = BudgetTotal(s);
            if (total.Min <= cap && total.Max > cap)
                result.Add(Term.A("presupuesto_puede_excederse"));
        }

        foreach (var o in s.Objects)
            if (Product(o.CatalogId) is { } p && !p.Styles.Any(e => _rules.StylesCompatible(s.Design.Style, e)))
                result.Add(Term.C("estilo_incompatible", Term.A(o.InstanceId)));

        foreach (var r in s.Requirements)
            if (r.Priority is "importante" or "preferencia" && _rules.EvaluableRequirements.Contains(r.Type) && !Meets(s, r))
                result.Add(Term.C("requisito_no_cumplido", Term.A(r.Priority), RequirementTerm(r)));

        if (_rules.Basics.TryGetValue(s.Room.SpaceType, out var basics))
            foreach (var cat in basics)
                if (!s.Objects.Any(o => Product(o.CatalogId)?.Category == cat))
                    result.Add(Term.C("falta_basico", Term.A(cat)));

        foreach (var o in s.Objects)
            if (Product(o.CatalogId) is { IsConceptual: true })
                result.Add(Term.C("elemento_conceptual", Term.A(o.InstanceId)));

        return result;
    }

    /// <summary>diseno_valido/1: sin conflictos.</summary>
    public bool IsValid(DesignState s) => Conflicts(s).Count == 0;

    // ---------------------------------------------------------------- propuestas

    /// <summary>motivo_colocacion/7: por que no se puede poner el producto en (x,y,rot).</summary>
    private IEnumerable<Term> PlacementReasons(DesignState s, string? excluded, ProductState p, int x, int y, int rot)
    {
        var room = s.Room;
        var r = Geometry.Footprint(p.Dimensions, x, y, rot);
        if (!Geometry.Inside(room, r))
            yield return Term.A("fuera_de_habitacion");
        if (!OccupiesFloor(p))
            yield break;
        foreach (var other in Placed(s))
            if (other.Obj.InstanceId != excluded && OccupiesFloor(other.Product) && r.Overlaps(other.Rect))
                yield return Term.C("colision_con", Term.A(other.Obj.InstanceId));
        foreach (var door in room.Doors)
            if (r.Overlaps(Geometry.DoorZone(room, door)))
                yield return Term.C("bloquea_puerta", Term.A(door.Id));
        foreach (var window in room.Windows)
            if (p.Dimensions.Height > window.SillHeight && r.Overlaps(Geometry.WindowZone(room, window, _rules.WindowAccessCm)))
                yield return Term.C("bloquea_ventana", Term.A(window.Id));
    }

    private bool Fits(RoomState room, ProductState p)
    {
        int w = p.Dimensions.Width, d = p.Dimensions.Depth;
        return (w <= room.Length && d <= room.Width) || (d <= room.Length && w <= room.Width);
    }

    /// <summary>
    /// validar_propuesta/3. Devuelve null cuando la accion no aplica
    /// (en Prolog el predicado falla y veredicto/3 responde accion_invalida).
    /// </summary>
    public List<Term>? ValidateProposal(DesignState s, Proposal proposal)
    {
        switch (proposal)
        {
            case MoveProposal m:
            {
                var obj = s.Objects.FirstOrDefault(o => o.InstanceId == m.InstanceId);
                if (obj is null) return null;
                var reasons = Product(obj.CatalogId) is { } p
                    ? PlacementReasons(s, m.InstanceId, p, m.X, m.Y, m.Rotation).ToList()
                    : [];
                if (obj.Locked) reasons.Insert(0, Term.C("elemento_bloqueado", Term.A(m.InstanceId)));
                return reasons;
            }
            case AddProposal a:
            {
                var reasons = new List<Term>();
                var p = Product(a.CatalogId);
                if (s.Objects.Any(o => o.InstanceId == a.InstanceId)) reasons.Add(Term.A("instancia_repetida"));
                if (p is null) reasons.Add(Term.A("producto_inexistente"));
                if (p is not null && !_rules.CategoryAllowed(s.Room.SpaceType, p.Category)) reasons.Add(Term.A("categoria_no_permitida"));
                if (p is not null && !Fits(s.Room, p)) reasons.Add(Term.A("no_cabe"));
                if (p is not null) reasons.AddRange(PlacementReasons(s, a.InstanceId, p, a.X, a.Y, a.Rotation));
                if (p is not null && EffectiveCap(s) is { } cap && BudgetTotal(s).Min + p.Price.Min > cap)
                    reasons.Add(Term.A("presupuesto_excedido"));
                return reasons;
            }
            case ReplaceProposal r:
            {
                var obj = s.Objects.FirstOrDefault(o => o.InstanceId == r.InstanceId);
                if (obj is null) return null;
                var actual = Product(obj.CatalogId);
                var nuevo = Product(r.CatalogId);
                var reasons = new List<Term>();
                if (obj.Locked) reasons.Add(Term.C("elemento_bloqueado", Term.A(r.InstanceId)));
                if (nuevo is null) reasons.Add(Term.A("producto_inexistente"));
                if (actual is not null && nuevo is not null && actual.Category != nuevo.Category)
                    reasons.Add(Term.A("categoria_distinta"));
                if (nuevo is not null)
                    reasons.AddRange(PlacementReasons(s, r.InstanceId, nuevo, obj.Position.X, obj.Position.Y, obj.Rotation));
                if (actual is not null && nuevo is not null && EffectiveCap(s) is { } cap
                    && BudgetTotal(s).Min - actual.Price.Min + nuevo.Price.Min > cap)
                    reasons.Add(Term.A("presupuesto_excedido"));
                if (actual is not null)
                    foreach (var req in s.Requirements.Where(q => q.Priority == "obligatorio" && q.Type == "tiene" && q.Category == actual.Category))
                        if (!AttributeEquals(nuevo, req.Attribute!, req.Value))
                            reasons.Add(Term.C("rompe_requisito", RequirementTerm(req)));
                return reasons;
            }
            case RemoveProposal q:
            {
                var obj = s.Objects.FirstOrDefault(o => o.InstanceId == q.InstanceId);
                if (obj is null || Product(obj.CatalogId) is not { } p) return null;
                var reasons = new List<Term>();
                if (obj.Locked) reasons.Add(Term.C("elemento_bloqueado", Term.A(q.InstanceId)));
                foreach (var req in s.Requirements.Where(r => r.Priority == "obligatorio" && r.Type == "tiene" && r.Category == p.Category))
                    if (AttributeEquals(p, req.Attribute!, req.Value)
                        && !s.Objects.Any(o => o.InstanceId != q.InstanceId && AttributeEquals(Product(o.CatalogId), req.Attribute!, req.Value)))
                        reasons.Add(Term.C("rompe_requisito", RequirementTerm(req)));
                return reasons;
            }
            case ModifyStructureProposal e:
                if (!_rules.StructuralElements.Contains(e.Element))
                    return [Term.C("no_es_elemento_estructural", Term.A(e.Element))];
                return s.StructuralConfirmations.Contains(e.Element)
                    ? []
                    : [Term.C("estructural_protegido", Term.A(e.Element))];
            default:
                return null;
        }
    }

    /// <summary>veredicto/3</summary>
    public Verdict Judge(DesignState s, Proposal proposal) =>
        ValidateProposal(s, proposal) is { } reasons ? Verdict.From(reasons) : Verdict.Invalid;

    // ---------------------------------------------------------------- colocar un mueble nuevo

    private static readonly HashSet<string> PositionReasons = ["fuera_de_habitacion", "colision_con", "bloquea_puerta", "bloquea_ventana"];

    /// <summary>
    /// Busca donde poner un producto nuevo: recorre la habitacion en una rejilla (cm) y las 4 rotaciones,
    /// del centro hacia afuera, y devuelve la primera posicion sin motivos de colocacion.
    /// Si el producto no se puede agregar por algo que no depende del lugar (presupuesto, categoria,
    /// no cabe), devuelve el veredicto con esos motivos y sin posicion.
    /// </summary>
    public (AddProposal? Placement, Verdict Verdict) SuggestPlacement(DesignState s, string instanceId, string catalogId, int stepCm = 10)
    {
        var probe = Judge(s, new AddProposal(instanceId, catalogId, 0, 0, 0));
        var blocking = probe.Reasons.Where(r => !PositionReasons.Contains(r.Name)).ToList();
        if (blocking.Count > 0 || Product(catalogId) is not { } p)
            return (null, Verdict.From(blocking.Count > 0 ? blocking : probe.Reasons));

        var grid =
            from rot in new[] { 0, 90, 180, 270 }
            let dims = Geometry.Rotated(rot, p.Dimensions.Width, p.Dimensions.Depth)
            from x in Enumerable.Range(0, Math.Max(0, (s.Room.Length - dims.A) / stepCm) + 1).Select(i => i * stepCm)
            from y in Enumerable.Range(0, Math.Max(0, (s.Room.Width - dims.B) / stepCm) + 1).Select(i => i * stepCm)
            select (Proposal: new AddProposal(instanceId, catalogId, x, y, rot), Score: PlacementScore(s.Room, p, x, y, rot, dims));

        var candidates = PreferredSpots(s, instanceId, p).Concat(grid.OrderBy(c => c.Score).Select(c => c.Proposal));
        foreach (var c in candidates)
        {
            var v = Judge(s, c);
            if (v.Accepted) return (c, v);
        }
        return (null, Verdict.From([Term.A("sin_espacio_libre")]));
    }

    /// <summary>[SUPUESTO] como se acomoda cada categoria al agregarla.</summary>
    private static readonly HashSet<string> AgainstWall = ["sofa", "cama", "mueble_tv", "librero", "escritorio", "cuadro"];
    private static readonly HashSet<string> InCorner = ["planta", "lampara"];

    /// <summary>
    /// Puntaje de un lugar (menor = mejor). Contra la pared: la espalda pegada al muro que le toca segun su
    /// rotacion (0 mira al sur -> espalda al norte; 90 al este -> espalda al oeste; 180 -> sur; 270 -> este),
    /// centrado a lo largo del muro. En esquina: cerca de una esquina. Lo demas: cerca del centro.
    /// </summary>
    private static double PlacementScore(RoomState room, ProductState p, int x, int y, int rot, (int A, int B) dims)
    {
        double cx = x + dims.A / 2.0, cy = y + dims.B / 2.0;
        if (AgainstWall.Contains(p.Category))
        {
            var (gap, along, wallMid) = rot switch
            {
                0 => (room.Width - (y + dims.B), cx, room.Length / 2.0),
                90 => (x, cy, room.Width / 2.0),
                180 => (y, cx, room.Length / 2.0),
                _ => (room.Length - (x + dims.A), cy, room.Width / 2.0)
            };
            return gap * 10 + Math.Abs(along - wallMid);
        }
        if (InCorner.Contains(p.Category))
        {
            double dx = Math.Min(cx, room.Length - cx), dy = Math.Min(cy, room.Width - cy);
            return dx * dx + dy * dy;
        }
        return Math.Pow(cx - room.Length / 2.0, 2) + Math.Pow(cy - room.Width / 2.0, 2);
    }

    /// <summary>La TV se propone primero centrada sobre un mueble de TV que ya este en el diseno.</summary>
    private IEnumerable<AddProposal> PreferredSpots(DesignState s, string instanceId, ProductState p)
    {
        if (p.Category != "televisor") yield break;
        foreach (var (obj, cabinet, rect) in Placed(s))
        {
            if (cabinet.Category != "mueble_tv") continue;
            var (a, b) = Geometry.Rotated(obj.Rotation, p.Dimensions.Width, p.Dimensions.Depth);
            int x = rect.X1 + (rect.X2 - rect.X1 - a) / 2, y = rect.Y1 + (rect.Y2 - rect.Y1 - b) / 2;
            yield return new AddProposal(instanceId, p.Id, x, y, obj.Rotation);
        }
    }

    // ---------------------------------------------------------------- similares y reemplazo

    /// <summary>similar/2: misma categoria y al menos un estilo en comun.</summary>
    public IEnumerable<ProductState> Similar(string productId)
    {
        if (Product(productId) is not { } a) yield break;
        foreach (var b in catalog.Products)
            if (b.Category == a.Category && b.Id != a.Id && a.Styles.Any(e => b.Styles.Contains(e)))
                yield return b;
    }

    /// <summary>buscar_similares/2: ordenados por precio minimo (orden estable).</summary>
    public List<ProductState> SimilarByPrice(string productId) =>
        Similar(productId).OrderBy(p => p.Price.Min).ToList();

    /// <summary>alternativas/3: similares que si se pueden poner en el lugar del objeto.</summary>
    public List<ProductState> Alternatives(DesignState s, string instanceId)
    {
        var obj = s.Objects.FirstOrDefault(o => o.InstanceId == instanceId);
        if (obj is null) return [];
        return Similar(obj.CatalogId)
            .Where(n => ValidateProposal(s, new ReplaceProposal(instanceId, n.Id)) is { Count: 0 })
            .ToList();
    }

    /// <summary>sustitucion_ahorro/4: reemplazos reales, validos y mas baratos (Ahorro sobre el precio minimo).</summary>
    public List<(string InstanceId, ProductState Product, decimal Saving)> Savings(DesignState s)
    {
        var result = new List<(string, ProductState, decimal)>();
        foreach (var obj in s.Objects)
        {
            if (obj.Locked || Product(obj.CatalogId) is not { } actual) continue;
            foreach (var n in Similar(actual.Id))
                if (n.Price.Min < actual.Price.Min && !n.IsConceptual
                    && ValidateProposal(s, new ReplaceProposal(obj.InstanceId, n.Id)) is { Count: 0 })
                    result.Add((obj.InstanceId, n, actual.Price.Min - n.Price.Min));
        }
        return result;
    }

    // ---------------------------------------------------------------- versiones

    /// <summary>comparar_versiones/3: diferencias por categoria, ordenadas y sin repetidos.</summary>
    public List<Term> Compare(DesignState a, DesignState b)
    {
        List<(string Cat, string Id)> ByCat(DesignState s) =>
            s.Objects.Select(o => Product(o.CatalogId)).Where(p => p is not null).Select(p => (p!.Category, p.Id)).ToList();

        var la = ByCat(a);
        var lb = ByCat(b);
        var difs = new List<(string, string, string)>();
        foreach (var (cat, idA) in la)
        {
            var idB = lb.FirstOrDefault(x => x.Cat == cat).Id ?? "ninguno";
            if (idA != idB) difs.Add((cat, idA, idB));
        }
        foreach (var (cat, idB) in lb)
            if (!la.Any(x => x.Cat == cat))
                difs.Add((cat, "ninguno", idB));

        return difs.Distinct()
            .OrderBy(d => d.Item1, StringComparer.Ordinal)
            .ThenBy(d => d.Item2, StringComparer.Ordinal)
            .ThenBy(d => d.Item3, StringComparer.Ordinal)
            .Select(d => Term.C("dif", Term.A(d.Item1), Term.A(d.Item2), Term.A(d.Item3)))
            .ToList();
    }
}
