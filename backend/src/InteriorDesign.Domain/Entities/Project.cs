namespace InteriorDesign.Domain.Entities;

public class Project
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    /// <summary>comercial | domestico</summary>
    public required string Type { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Room> Rooms { get; set; } = [];
    public List<Design> Designs { get; set; } = [];
}
