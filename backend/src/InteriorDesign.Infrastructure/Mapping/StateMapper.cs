using System.Text.Json;
using InteriorDesign.Domain.Entities;
using InteriorDesign.Domain.State;

namespace InteriorDesign.Infrastructure.Mapping;

/// <summary>Convierte entre el contrato JSON (DesignState, catalogo) y las entidades de la BD.</summary>
public static class StateMapper
{
    private static string? Json(JsonElement? e) => e?.GetRawText();
    private static JsonElement? Element(string? json) => json is null ? null : JsonDocument.Parse(json).RootElement.Clone();

    // ---------------------------------------------------------------- catalogo

    public static Product ToEntity(ProductState p) => new()
    {
        Id = p.Id, Name = p.Name, Category = p.Category, Kind = p.Kind,
        WidthCm = p.Dimensions.Width, DepthCm = p.Dimensions.Depth, HeightCm = p.Dimensions.Height,
        Materials = [.. p.Materials], Colors = [.. p.Colors], Styles = [.. p.Styles],
        PriceMin = p.Price.Min, PriceMax = p.Price.Max,
        AttributesJson = JsonSerializer.Serialize(p.Attributes),
        ProviderId = p.ProviderId, Sku = p.Sku, Availability = p.Availability, Asset3D = p.Asset3D,
        Images = [.. p.Images], Variants = [.. p.Variants]
    };

    public static ProductState ToState(Product p) => new()
    {
        Id = p.Id, Name = p.Name, Category = p.Category, Kind = p.Kind,
        Dimensions = new Dimensions { Width = p.WidthCm, Depth = p.DepthCm, Height = p.HeightCm },
        Materials = [.. p.Materials], Colors = [.. p.Colors], Styles = [.. p.Styles],
        Price = new PriceRange { Min = p.PriceMin, Max = p.PriceMax },
        Attributes = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(p.AttributesJson) ?? [],
        ProviderId = p.ProviderId, Sku = p.Sku, Availability = p.Availability, Asset3D = p.Asset3D,
        Images = [.. p.Images], Variants = [.. p.Variants]
    };

    public static CatalogMaterial ToEntity(MaterialState m) => new()
    {
        Id = m.Id, Type = m.Type, Name = m.Name,
        PricePerM2Min = m.PricePerM2.Min, PricePerM2Max = m.PricePerM2.Max,
        Color = m.Color, Finish = m.Finish, Texture = m.Texture, ProviderId = m.ProviderId, Sku = m.Sku
    };

    public static MaterialState ToState(CatalogMaterial m) => new()
    {
        Id = m.Id, Type = m.Type, Name = m.Name,
        PricePerM2 = new PriceRange { Min = m.PricePerM2Min, Max = m.PricePerM2Max },
        Color = m.Color, Finish = m.Finish, Texture = m.Texture, ProviderId = m.ProviderId, Sku = m.Sku
    };

    public static Provider ToEntity(ProviderState p) => new()
    {
        Id = p.Id, Name = p.Name, BudgetCategory = p.BudgetCategory, Adapter = p.Adapter
    };

    public static ProviderState ToState(Provider p) => new()
    {
        Id = p.Id, Name = p.Name, BudgetCategory = p.BudgetCategory, Adapter = p.Adapter
    };

    // ---------------------------------------------------------------- habitacion y requisitos

    public static Room ToEntity(RoomState r, string projectId, IEnumerable<RequirementState> requirements) => new()
    {
        Id = r.Id, ProjectId = projectId, Name = r.Name, SpaceType = r.SpaceType,
        LengthCm = r.Length, WidthCm = r.Width, HeightCm = r.Height, MeasurementSource = r.MeasurementSource,
        SurfacesJson = Json(r.Surfaces), CommercialJson = Json(r.Commercial),
        Doors = r.Doors.Select(d => new RoomDoor { RoomId = r.Id, Id = d.Id, Wall = d.Wall, PositionCm = d.Position, WidthCm = d.Width }).ToList(),
        Windows = r.Windows.Select(w => new RoomWindow
        {
            RoomId = r.Id, Id = w.Id, Wall = w.Wall, PositionCm = w.Position, WidthCm = w.Width,
            SillHeightCm = w.SillHeight, HeightCm = w.Height
        }).ToList(),
        Columns = r.Columns.Select(c => new RoomColumn
        {
            RoomId = r.Id, Id = c.Id, XCm = c.X, YCm = c.Y, WidthCm = c.Width, DepthCm = c.Depth
        }).ToList(),
        Requirements = requirements.Select(q => ToEntity(q, r.Id)).ToList()
    };

    public static Requirement ToEntity(RequirementState q, string roomId) => new()
    {
        RoomId = roomId, Id = q.Id, Priority = q.Priority, Type = q.Type, Description = q.Description,
        Amount = q.Amount, Category = q.Category, Attribute = q.Attribute, ValueJson = Json(q.Value),
        Element = q.Element, Style = q.Style, Color = q.Color
    };

    public static RoomState ToState(Room r) => new()
    {
        Id = r.Id, Name = r.Name, SpaceType = r.SpaceType,
        Length = r.LengthCm, Width = r.WidthCm, Height = r.HeightCm, MeasurementSource = r.MeasurementSource,
        Surfaces = Element(r.SurfacesJson), Commercial = Element(r.CommercialJson),
        Doors = r.Doors.OrderBy(d => d.Id, StringComparer.Ordinal)
            .Select(d => new DoorState { Id = d.Id, Wall = d.Wall, Position = d.PositionCm, Width = d.WidthCm }).ToList(),
        Windows = r.Windows.OrderBy(w => w.Id, StringComparer.Ordinal)
            .Select(w => new WindowState { Id = w.Id, Wall = w.Wall, Position = w.PositionCm, Width = w.WidthCm, SillHeight = w.SillHeightCm, Height = w.HeightCm }).ToList(),
        Columns = r.Columns.OrderBy(c => c.Id, StringComparer.Ordinal)
            .Select(c => new ColumnState { Id = c.Id, X = c.XCm, Y = c.YCm, Width = c.WidthCm, Depth = c.DepthCm }).ToList()
    };

    public static RequirementState ToState(Requirement q) => new()
    {
        Id = q.Id, Priority = q.Priority, Type = q.Type, Description = q.Description,
        Amount = q.Amount, Category = q.Category, Attribute = q.Attribute, Value = Element(q.ValueJson),
        Element = q.Element, Style = q.Style, Color = q.Color
    };

    // ---------------------------------------------------------------- diseno

    /// <summary>
    /// Entidad Design a partir del DesignState (sin habitacion ni historial: se guardan aparte).
    /// El orden de los objetos se conserva con Order para que el motor de el mismo resultado.
    /// </summary>
    public static Design ToEntity(DesignState s)
    {
        var d = s.Design;
        return new Design
        {
            Id = d.Id, ProjectId = s.Project.Id, RoomId = s.Room.Id, Name = d.Name, ParentId = d.ParentId,
            Style = d.Style, BudgetMode = d.BudgetMode, Revision = d.Revision, Author = d.Author,
            Favorite = d.Favorite, Comments = d.Comments, ThumbnailUrl = d.ThumbnailUrl, RenderUrl = d.RenderUrl,
            InstallationMin = s.Installation?.Min, InstallationMax = s.Installation?.Max,
            LightingJson = Json(s.Lighting),
            StructuralConfirmations = [.. s.StructuralConfirmations],
            Objects = s.Objects.Select((o, i) => ToEntity(o, d.Id, i)).ToList(),
            Materials = s.Materials.Select(m => new DesignMaterial
            {
                DesignId = d.Id, MaterialId = m.MaterialId, AreaM2 = m.AreaM2, Surface = m.Surface
            }).ToList()
        };
    }

    public static DesignObject ToEntity(PlacedObject o, string designId, int order) => new()
    {
        DesignId = designId, InstanceId = o.InstanceId, CatalogId = o.CatalogId, Type = o.Type,
        XCm = o.Position.X, YCm = o.Position.Y, ZCm = o.Position.Z, Rotation = o.Rotation,
        ScaleX = o.Scale?.X ?? 1, ScaleY = o.Scale?.Y ?? 1, ScaleZ = o.Scale?.Z ?? 1,
        Color = o.Config?.Color, Material = o.Config?.Material, Locked = o.Locked, SortOrder = order
    };

    /// <summary>DesignState completo. La entidad debe venir con Project, Room (y sus hijos), Objects, Materials e History.</summary>
    public static DesignState ToState(Design d)
    {
        var room = d.Room ?? throw new InvalidOperationException("Falta cargar Room del diseno.");
        var project = d.Project ?? throw new InvalidOperationException("Falta cargar Project del diseno.");
        return new DesignState
        {
            Project = new ProjectInfo { Id = project.Id, Name = project.Name, Type = project.Type },
            Design = new DesignInfo
            {
                Id = d.Id, Name = d.Name, ParentId = d.ParentId, Style = d.Style, BudgetMode = d.BudgetMode,
                Revision = d.Revision, Author = d.Author,
                CreatedAt = DateTime.SpecifyKind(d.CreatedAt, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(d.UpdatedAt, DateTimeKind.Utc),
                Favorite = d.Favorite, Comments = d.Comments, ThumbnailUrl = d.ThumbnailUrl, RenderUrl = d.RenderUrl
            },
            Room = ToState(room),
            Requirements = room.Requirements.OrderBy(q => q.Id, StringComparer.Ordinal).Select(ToState).ToList(),
            Objects = d.Objects.OrderBy(o => o.SortOrder).ThenBy(o => o.InstanceId, StringComparer.Ordinal).Select(o => new PlacedObject
            {
                InstanceId = o.InstanceId, CatalogId = o.CatalogId, Type = o.Type,
                Position = new Position { X = o.XCm, Y = o.YCm, Z = o.ZCm }, Rotation = o.Rotation,
                Scale = o.ScaleX == 1 && o.ScaleY == 1 && o.ScaleZ == 1 ? null : new Scale { X = o.ScaleX, Y = o.ScaleY, Z = o.ScaleZ },
                Config = o.Color is null && o.Material is null ? null : new ObjectConfig { Color = o.Color, Material = o.Material },
                Locked = o.Locked
            }).ToList(),
            Materials = d.Materials.OrderBy(m => m.Id).Select(m => new AppliedMaterial
            {
                MaterialId = m.MaterialId, AreaM2 = m.AreaM2, Surface = m.Surface
            }).ToList(),
            Lighting = Element(d.LightingJson),
            Installation = d.InstallationMin is { } min && d.InstallationMax is { } max ? new PriceRange { Min = min, Max = max } : null,
            StructuralConfirmations = [.. d.StructuralConfirmations],
            History = d.History.OrderBy(h => h.Date).ThenBy(h => h.Id).Select(h => new HistoryEntryState
            {
                Date = DateTime.SpecifyKind(h.Date, DateTimeKind.Utc), Author = h.Author, Description = h.Description,
                ChangedInstanceIds = [.. h.ChangedInstanceIds], Revision = h.Revision
            }).ToList()
        };
    }
}
