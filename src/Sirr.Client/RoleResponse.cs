#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// A role definition in the multi-tenant system.
/// </summary>
public sealed record RoleResponse
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Permission letter string e.g. "CWRD" (Create, Write, Read, Delete).</summary>
    [JsonPropertyName("permissions")]
    public required string Permissions { get; init; }

    [JsonPropertyName("org_id")]
    public string? OrgId { get; init; }

    /// <summary>True for built-in roles (reader, writer, admin) that cannot be deleted.</summary>
    [JsonPropertyName("built_in")]
    public bool BuiltIn { get; init; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }
}
