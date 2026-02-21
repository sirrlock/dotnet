namespace Sirr;

/// <summary>
/// Async client for the Sirr ephemeral secrets API.
/// </summary>
public interface ISirrClient
{
    /// <summary>
    /// Checks if the Sirr server is healthy. Does not require authentication.
    /// </summary>
    Task<bool> HealthAsync(CancellationToken ct = default);

    /// <summary>
    /// Stores a secret with optional TTL and read limit.
    /// </summary>
    Task PushAsync(string key, string value, TimeSpan? ttl = null, int? reads = null, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a secret value. Returns <c>null</c> if the secret is burned, expired, or does not exist.
    /// </summary>
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes a secret. Returns <c>true</c> if deleted, <c>false</c> if it did not exist.
    /// </summary>
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Lists metadata for all active secrets. Values are never included.
    /// </summary>
    Task<IReadOnlyList<SecretMeta>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Pulls all secrets into a dictionary of key-value pairs.
    /// Secrets that are burned during retrieval are silently skipped.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> PullAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Prunes expired secrets from the server. Returns the number of secrets pruned.
    /// </summary>
    Task<int> PruneAsync(CancellationToken ct = default);

    /// <summary>
    /// Pulls all secrets and sets them as environment variables.
    /// Dispose the returned scope to restore original values.
    /// </summary>
    Task<EnvScope> CreateEnvScopeAsync(CancellationToken ct = default);
}
