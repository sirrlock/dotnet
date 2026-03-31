using System.Text.Json.Serialization;

namespace Sirr;

/// <summary>
/// Result of a public <see cref="ISirrClient.PushAsync"/> call.
/// </summary>
public sealed class PushResponse
{
    /// <summary>
    /// The server-assigned 64-character hex ID for the dead-drop secret.
    /// Use this ID to retrieve the secret via <see cref="ISirrClient.GetAsync"/>.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}
