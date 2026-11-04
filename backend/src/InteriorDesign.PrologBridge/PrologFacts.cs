using System.Text;
using InteriorDesign.Domain.State;
using InteriorDesign.Engine;

namespace InteriorDesign.PrologBridge;

/// <summary>
/// Convierte DesignStates y el catalogo en hechos de Prolog con la misma forma que diseno.pl
/// (habitacion/5, objeto/3, coloca/5, producto/8...). Los textos salen con writeq (Term).
/// </summary>
public static class PrologFacts
{
    private static string A(string atom) => Term.A(atom).ToString();
    private static string N(decimal number) => Term.N(number).ToString();
    private static string Fact(string functor, params string[] args) => $"{A(functor)}({string.Join(",", args)}).";
    private static string List(IEnumerable<string> atoms) => "[" + string.Join(",", atoms.Select(A)) + "]";

    public static string Build(IReadOnlyCollection<DesignState> states, CatalogState catalog)
    {
        var sb = new StringBuilder();
        var rooms = new HashSet<string>();
        var designIds = states.Select(s => s.Design.Id).ToHashSet();

        foreach (var s in states)
        {
            var room = s.Room;
            if (rooms.Add(room.Id))
            {
                sb.AppendLine(Fact("habitacion", A(room.Id), A(room.SpaceType), N(room.Length), N(room.Width), N(room.Height)));
                foreach (var d in room.Doors)
                    sb.AppendLine(Fact("puerta", A(room.Id), A(d.Id), A(d.Wall), N(d.Position), N(d.Width)));
                foreach (var w in room.Windows)
                    sb.AppendLine(Fact("ventana", A(room.Id), A(w.Id), A(w.Wall), N(w.Position), N(w.Width), N(w.SillHeight)));
                foreach (var r in s.Requirements)
                    sb.AppendLine(Fact("requisito", A(room.Id), A(r.Priority), DesignEngine.RequirementTerm(r).ToString()));
            }

            var d0 = s.Design;
            sb.AppendLine(Fact("diseno", A(d0.Id), A(room.Id), A(d0.Style)));
            if (d0.ParentId is { } parent && designIds.Contains(parent))
                sb.AppendLine(Fact("version_padre", A(d0.Id), A(parent)));
            sb.AppendLine(Fact("modo_presupuesto", A(d0.Id), A(d0.BudgetMode)));

            foreach (var o in s.Objects)
            {
                sb.AppendLine(Fact("objeto", A(d0.Id), A(o.InstanceId), A(o.CatalogId)));
                sb.AppendLine(Fact("coloca", A(d0.Id), A(o.InstanceId), N(o.Position.X), N(o.Position.Y), N(o.Rotation)));
                if (o.Config?.Color is { } color)
                    sb.AppendLine(Fact("config", A(d0.Id), A(o.InstanceId), "color", A(color)));
                if (o.Locked)
                    sb.AppendLine(Fact("bloqueado", A(d0.Id), A(o.InstanceId)));
            }
            foreach (var m in s.Materials)
                sb.AppendLine(Fact("aplica_material", A(d0.Id), A(m.MaterialId), N(m.AreaM2)));
            if (s.Installation is { } inst)
                sb.AppendLine(Fact("instalacion", A(d0.Id), $"precio({N(inst.Min)},{N(inst.Max)})"));
            foreach (var e in s.StructuralConfirmations)
                sb.AppendLine(Fact("confirmacion_usuario", A(d0.Id), A(e)));
        }

        foreach (var p in catalog.Products)
        {
            sb.AppendLine(Fact("producto", A(p.Id), A(p.Name), A(p.Category),
                $"dim({N(p.Dimensions.Width)},{N(p.Dimensions.Depth)},{N(p.Dimensions.Height)})",
                List(p.Materials), List(p.Colors), List(p.Styles),
                $"precio({N(p.Price.Min)},{N(p.Price.Max)})"));
            if (p.IsConceptual)
                sb.AppendLine(Fact("es_conceptual", A(p.Id)));
            foreach (var (k, v) in p.Attributes)
            {
                var value = v.ValueKind == System.Text.Json.JsonValueKind.Number ? N(v.GetDecimal()) : A(v.GetString() ?? "");
                sb.AppendLine(Fact("atributo", A(p.Id), A(k), value));
            }
        }
        foreach (var m in catalog.Materials)
            sb.AppendLine(Fact("material", A(m.Id), A(m.Type), A(m.Name), $"precio_m2({N(m.PricePerM2.Min)},{N(m.PricePerM2.Max)})"));

        return sb.ToString();
    }
}
