#pragma warning disable CS1591
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// Result of creating a personal API key via POST /me/keys.
/// The raw <see cref="Key"/> value is shown only once — store it immediately.
/// </summary>
public sealed record KeyCreateResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>The raw API key — only returned at creation time.</summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("valid_after")]
    public long ValidAfter { get; init; }

    [JsonPropertyName("valid_before")]
    public long ValidBefore { get; init; }
}
