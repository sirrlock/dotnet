#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// An organization in the multi-tenant system.
/// </summary>
public sealed record OrgResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }
}
