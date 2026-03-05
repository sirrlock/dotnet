#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// A principal (user or service account) in the multi-tenant system.
/// </summary>
public sealed record PrincipalResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("org")]
    public string? Org { get; init; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }
}
