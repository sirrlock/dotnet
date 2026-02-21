#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// A single audit log entry.
/// </summary>
public sealed record AuditEvent
{
    [JsonPropertyName("id")]
    public required long Id { get; init; }

    [JsonPropertyName("timestamp")]
    public required long Timestamp { get; init; }

    [JsonPropertyName("action")]
    public required string Action { get; init; }

    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("source_ip")]
    public required string SourceIp { get; init; }

    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    [JsonPropertyName("detail")]
    public string? Detail { get; init; }

    [JsonPropertyName("org_id")]
    public string? OrgId { get; init; }

    [JsonPropertyName("principal_id")]
    public string? PrincipalId { get; init; }
}
