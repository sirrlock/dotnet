#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// A registered webhook (signing secret redacted).
/// </summary>
public sealed record Webhook
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("events")]
    public required string[] Events { get; init; }

    [JsonPropertyName("created_at")]
    public required long CreatedAt { get; init; }
}
