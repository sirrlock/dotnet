namespace Sirr;

/// <summary>
/// Configuration options for <see cref="SirrClient"/>.
/// </summary>
public sealed class SirrOptions
{
    /// <summary>
    /// Base URL of the Sirr server. Defaults to <c>http://localhost:39999</c>.
    /// </summary>
    public string Server { get; set; } = "http://localhost:39999";

    /// <summary>
    /// Bearer token for authentication.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// When set, routes secret operations through /orgs/{Org}/secrets/*.
    /// </summary>
    public string? Org { get; set; }
}
