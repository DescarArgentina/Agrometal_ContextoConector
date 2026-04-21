using System.Collections.Generic;
using System.Text.Json.Serialization;

public class ContextoDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("grupos")]
    public List<GrupoDto> Grupos { get; set; } = new();
}

public class GrupoDto
{
    [JsonPropertyName("idGrupo")]
    public string IdGrupo { get; set; } = "";

    [JsonPropertyName("nombreGrupo")]
    public string NombreGrupo { get; set; } = "";

    [JsonPropertyName("obligatorio")]
    public bool Obligatorio { get; set; }

    [JsonPropertyName("selMultiple")]
    public bool SelMultiple { get; set; }

    [JsonPropertyName("detalle")]
    public List<DetalleDto> Detalle { get; set; } = new();
}

public class DetalleDto
{
    [JsonPropertyName("idCaract")]
    public string IdCaract { get; set; } = "";

    [JsonPropertyName("nombreCaract")]
    public string NombreCaract { get; set; } = "";
}
