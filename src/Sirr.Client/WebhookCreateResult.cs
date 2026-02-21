#pragma warning disable CS1591 // Record properties are self-documenting via JsonPropertyName
using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// Result of creating a webhook — includes the signing secret (shown once).
/// </summary>
public sealed record WebhookCreateResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("secret")]
    public required string Secret { get; init; }
}
