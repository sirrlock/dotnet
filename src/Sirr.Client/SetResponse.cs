using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// Result of an org-scoped <see cref="ISirrClient.SetAsync"/> call.
/// </summary>
public sealed class SetResponse
{
    /// <summary>
    /// The key under which the secret was stored.
    /// </summary>
    [JsonPropertyName("key")]
    public required string Key { get; init; }
}
