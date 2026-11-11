namespace InteriorDesign.Domain.Entities;

/// <summary>
/// Trabajo asincrono de IA (lo procesa n8n). Estados: pendiente, en_proceso, completado, fallido.
/// Tipos: referencia (copiar un diseno de una imagen), chat (instruccion en texto), imagen (como quedaria).
/// </summary>
public class AiJob
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    /// <summary>Version sobre la que se trabaja.</summary>
    public required string DesignId { get; set; }
    /// <summary>Version creada por el trabajo (diseno de referencia), si aplica.</summary>
    public string? ResultDesignId { get; set; }
    public string Status { get; set; } = "pendiente";
    public int Progress { get; set; }
    public string? Message { get; set; }
    /// <summary>Lo que se le pidio a la IA (sin imagenes en base64).</summary>
    public string? InputJson { get; set; }
    /// <summary>Respuesta procesada: propuestas con su veredicto, resumen, URL de imagen...</summary>
    public string? ResultJson { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
