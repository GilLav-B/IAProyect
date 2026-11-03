using System.Text.Json.Nodes;
using InteriorDesign.Domain.Entities;

namespace InteriorDesign.Engine.Tests;

/// <summary>
/// Convierte los JSON de schemas/examples (DesignState y catalogo) a entidades de BD.
/// Sirve para comprobar que cada campo del DesignState tiene donde guardarse.
/// </summary>
public static class ExampleImporter
{
    public static string SchemasDir { get; } = FindSchemasDir();

    private static string FindSchemasDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "schemas");
            if (File.Exists(Path.Combine(candidate, "design-state.schema.json")))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("No se encontro la carpeta schemas/ del repositorio.");
    }

    public static JsonNode Load(string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(SchemasDir, "examples", file)))!;

    private static string[] Strings(JsonNode? node) =>
        node?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? [];

    private static string? Json(JsonNode? node) => node?.ToJsonString();

    public static (List<Provider> Providers, List<Product> Products, List<CatalogMaterial> Materials) Catalog(JsonNode c)
    {
        var providers = c["providers"]?.AsArray().Select(p => new Provider
        {
            Id = p!["id"]!.GetValue<string>(),
            Name = p["name"]!.GetValue<string>(),
            BudgetCategory = p["budgetCategory"]!.GetValue<string>(),
            Adapter = p["adapter"]!.GetValue<string>()
        }).ToList() ?? [];

        var products = c["products"]!.AsArray().Select(p => new Product
        {
            Id = p!["id"]!.GetValue<string>(),
            Name = p["name"]!.GetValue<string>(),
            Category = p["category"]!.GetValue<string>(),
            Kind = p["kind"]!.GetValue<string>(),
            WidthCm = p["dimensions"]!["width"]!.GetValue<int>(),
            DepthCm = p["dimensions"]!["depth"]!.GetValue<int>(),
            HeightCm = p["dimensions"]!["height"]!.GetValue<int>(),
            Materials = Strings(p["materials"]),
            Colors = Strings(p["colors"]),
            Styles = Strings(p["styles"]),
            PriceMin = p["price"]!["min"]!.GetValue<decimal>(),
            PriceMax = p["price"]!["max"]!.GetValue<decimal>(),
            AttributesJson = Json(p["attributes"]) ?? "{}",
            ProviderId = p["providerId"]?.GetValue<string>(),
            Sku = p["sku"]?.GetValue<string>(),
            Availability = p["availability"]?.GetValue<string>(),
            Asset3D = p["asset3D"]?.GetValue<string>(),
            Images = Strings(p["images"]),
            Variants = Strings(p["variants"])
        }).ToList();

        var materials = c["materials"]?.AsArray().Select(m => new CatalogMaterial
        {
            Id = m!["id"]!.GetValue<string>(),
            Type = m["type"]!.GetValue<string>(),
            Name = m["name"]!.GetValue<string>(),
            PricePerM2Min = m["pricePerM2"]!["min"]!.GetValue<decimal>(),
            PricePerM2Max = m["pricePerM2"]!["max"]!.GetValue<decimal>()
        }).ToList() ?? [];

        return (providers, products, materials);
    }

    public static Project Project(JsonNode s) => new()
    {
        Id = s["project"]!["id"]!.GetValue<string>(),
        Name = s["project"]!["name"]!.GetValue<string>(),
        Type = s["project"]!["type"]!.GetValue<string>()
    };

    public static Room Room(JsonNode s)
    {
        var r = s["room"]!;
        var roomId = r["id"]!.GetValue<string>();
        return new Room
        {
            Id = roomId,
            ProjectId = s["project"]!["id"]!.GetValue<string>(),
            Name = r["name"]?.GetValue<string>(),
            SpaceType = r["spaceType"]!.GetValue<string>(),
            LengthCm = r["length"]!.GetValue<int>(),
            WidthCm = r["width"]!.GetValue<int>(),
            HeightCm = r["height"]!.GetValue<int>(),
            MeasurementSource = r["measurementSource"]?.GetValue<string>() ?? "medidas_usuario",
            SurfacesJson = Json(r["surfaces"]),
            CommercialJson = Json(r["commercial"]),
            Doors = r["doors"]!.AsArray().Select(d => new RoomDoor
            {
                RoomId = roomId,
                Id = d!["id"]!.GetValue<string>(),
                Wall = d["wall"]!.GetValue<string>(),
                PositionCm = d["position"]!.GetValue<int>(),
                WidthCm = d["width"]!.GetValue<int>()
            }).ToList(),
            Windows = r["windows"]!.AsArray().Select(w => new RoomWindow
            {
                RoomId = roomId,
                Id = w!["id"]!.GetValue<string>(),
                Wall = w["wall"]!.GetValue<string>(),
                PositionCm = w["position"]!.GetValue<int>(),
                WidthCm = w["width"]!.GetValue<int>(),
                SillHeightCm = w["sillHeight"]!.GetValue<int>(),
                HeightCm = w["height"]?.GetValue<int>()
            }).ToList(),
            Columns = r["columns"]?.AsArray().Select(c => new RoomColumn
            {
                RoomId = roomId,
                Id = c!["id"]!.GetValue<string>(),
                XCm = c["x"]!.GetValue<int>(),
                YCm = c["y"]!.GetValue<int>(),
                WidthCm = c["width"]!.GetValue<int>(),
                DepthCm = c["depth"]!.GetValue<int>()
            }).ToList() ?? [],
            Requirements = s["requirements"]!.AsArray().Select(q => new Requirement
            {
                RoomId = roomId,
                Id = q!["id"]!.GetValue<string>(),
                Priority = q["priority"]!.GetValue<string>(),
                Type = q["type"]!.GetValue<string>(),
                Description = q["description"]?.GetValue<string>(),
                Amount = q["amount"]?.GetValue<decimal>(),
                Category = q["category"]?.GetValue<string>(),
                Attribute = q["attribute"]?.GetValue<string>(),
                ValueJson = Json(q["value"]),
                Element = q["element"]?.GetValue<string>(),
                Style = q["style"]?.GetValue<string>(),
                Color = q["color"]?.GetValue<string>()
            }).ToList()
        };
    }

    public static Design Design(JsonNode s)
    {
        var d = s["design"]!;
        var designId = d["id"]!.GetValue<string>();
        return new Design
        {
            Id = designId,
            ProjectId = s["project"]!["id"]!.GetValue<string>(),
            RoomId = s["room"]!["id"]!.GetValue<string>(),
            Name = d["name"]!.GetValue<string>(),
            ParentId = d["parentId"]?.GetValue<string>(),
            Style = d["style"]!.GetValue<string>(),
            BudgetMode = d["budgetMode"]!.GetValue<string>(),
            Revision = d["revision"]!.GetValue<int>(),
            Author = d["author"]?.GetValue<string>(),
            Favorite = d["favorite"]?.GetValue<bool>() ?? false,
            Comments = d["comments"]?.GetValue<string>(),
            ThumbnailUrl = d["thumbnailUrl"]?.GetValue<string>(),
            RenderUrl = d["renderUrl"]?.GetValue<string>(),
            InstallationMin = s["installation"]?["min"]?.GetValue<decimal>(),
            InstallationMax = s["installation"]?["max"]?.GetValue<decimal>(),
            LightingJson = Json(s["lighting"]),
            StructuralConfirmations = Strings(s["structuralConfirmations"]),
            Objects = s["objects"]!.AsArray().Select(o => new DesignObject
            {
                DesignId = designId,
                InstanceId = o!["instanceId"]!.GetValue<string>(),
                CatalogId = o["catalogId"]!.GetValue<string>(),
                Type = o["type"]!.GetValue<string>(),
                XCm = o["position"]!["x"]!.GetValue<int>(),
                YCm = o["position"]!["y"]!.GetValue<int>(),
                ZCm = o["position"]!["z"]?.GetValue<int>() ?? 0,
                Rotation = o["rotation"]!.GetValue<int>(),
                ScaleX = o["scale"]?["x"]?.GetValue<double>() ?? 1,
                ScaleY = o["scale"]?["y"]?.GetValue<double>() ?? 1,
                ScaleZ = o["scale"]?["z"]?.GetValue<double>() ?? 1,
                Color = o["config"]?["color"]?.GetValue<string>(),
                Material = o["config"]?["material"]?.GetValue<string>(),
                Locked = o["locked"]?.GetValue<bool>() ?? false
            }).ToList(),
            Materials = s["materials"]?.AsArray().Select(m => new DesignMaterial
            {
                DesignId = designId,
                MaterialId = m!["materialId"]!.GetValue<string>(),
                AreaM2 = m["areaM2"]!.GetValue<decimal>(),
                Surface = m["surface"]?.GetValue<string>()
            }).ToList() ?? [],
            History = s["history"]?.AsArray().Select(h => new DesignHistoryEntry
            {
                DesignId = designId,
                Date = DateTime.Parse(h!["date"]!.GetValue<string>(), null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal),
                Author = h["author"]?.GetValue<string>(),
                Description = h["description"]!.GetValue<string>(),
                ChangedInstanceIds = Strings(h["changedInstanceIds"]),
                Revision = h["revision"]?.GetValue<int>()
            }).ToList() ?? []
        };
    }
}
