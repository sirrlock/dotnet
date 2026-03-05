#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// Response from GET /me and PATCH /me.
/// GET populates all fields including <see cref="Keys"/> and <see cref="CreatedAt"/>.
/// PATCH returns <see cref="CreatedAt"/> as 0 and <see cref="Keys"/> as null.
/// </summary>
public sealed record MeResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("role")]
    public string? Role { get; init; }

    [JsonPropertyName("org_id")]
    public string? OrgId { get; init; }

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }

    [JsonPropertyName("keys")]
    public IReadOnlyList<PrincipalKey>? Keys { get; init; }
}
