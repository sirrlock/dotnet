namespace Sirr;

/// <summary>
/// Configuration options for <see cref="SirrClient"/>.
/// </summary>
public sealed class SirrOptions
{
    /// <summary>
    /// Base URL of the Sirr server. Defaults to <c>http://localhost:8080</c>.
    /// </summary>
    public string Server { get; set; } = "http://localhost:8080";

    /// <summary>
    /// Bearer token for authentication.
    /// </summary>
    public string Token { get; set; } = string.Empty;
}
