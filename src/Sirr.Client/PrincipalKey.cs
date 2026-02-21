#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// A personal API key belonging to the authenticated principal (returned by GET /me).
/// The raw key value is only available at creation time via <see cref="KeyCreateResult"/>.
/// </summary>
public sealed record PrincipalKey
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("valid_after")]
    public long ValidAfter { get; init; }

    [JsonPropertyName("valid_before")]
    public long ValidBefore { get; init; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }
}
