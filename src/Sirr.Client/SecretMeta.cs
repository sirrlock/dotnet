#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// Metadata for a stored secret. Values are never included in list responses.
/// </summary>
public sealed record SecretMeta
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }

    [JsonPropertyName("expires_at")]
    public long? ExpiresAt { get; init; }

    [JsonPropertyName("max_reads")]
    public int? MaxReads { get; init; }

    [JsonPropertyName("read_count")]
    public required int ReadCount { get; init; }

    /// <summary>
    /// <c>true</c> = burn-after-read (deleted when read budget exhausted).
    /// <c>false</c> = seal mode (blocked but patchable via <see cref="ISirrClient.PatchAsync"/>).
    /// </summary>
    [JsonPropertyName("delete")]
    public bool Delete { get; init; }
}
