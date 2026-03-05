#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// A role definition in the multi-tenant system.
/// </summary>
public sealed record RoleResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("permissions")]
    public required string[] Permissions { get; init; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }
}
