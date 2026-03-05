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
    /// When <paramref name="sealOnExpiry"/> is <c>true</c>, the secret enters seal mode
    /// (reads return 410 after the read budget is exhausted) and can be updated with
    /// <see cref="PatchAsync"/>. Defaults to <c>null</c> (server default: burn-after-read).
    /// </summary>
    Task PushAsync(string key, string value, TimeSpan? ttl = null, int? reads = null, bool? sealOnExpiry = null, CancellationToken ct = default);

    /// <summary>
    /// Updates the TTL or read budget of an existing secret without changing its value.
    /// Only works on secrets pushed with <c>sealOnExpiry: true</c>.
    /// </summary>
    Task PatchAsync(string key, TimeSpan? ttl = null, int? reads = null, CancellationToken ct = default);

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

    /// <summary>
    /// Queries the audit log with optional filters.
    /// </summary>
    Task<IReadOnlyList<AuditEvent>> GetAuditLogAsync(long? since = null, long? until = null, string? action = null, int? limit = null, CancellationToken ct = default);

    /// <summary>
    /// Registers a webhook endpoint.
    /// </summary>
    Task<WebhookCreateResult> CreateWebhookAsync(string url, string[]? events = null, CancellationToken ct = default);

    /// <summary>
    /// Lists all registered webhooks. Signing secrets are redacted.
    /// </summary>
    Task<IReadOnlyList<Webhook>> ListWebhooksAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes a webhook by ID. Returns <c>false</c> if it did not exist.
    /// </summary>
    Task<bool> DeleteWebhookAsync(string id, CancellationToken ct = default);

    // --- /me ---

    /// <summary>
    /// Gets the authenticated principal's profile.
    /// </summary>
    Task<MeResponse> GetMeAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the authenticated principal's profile.
    /// </summary>
    Task<MeResponse> UpdateMeAsync(string? name = null, string? email = null, CancellationToken ct = default);

    /// <summary>
    /// Creates a personal API key scoped to the authenticated principal.
    /// </summary>
    Task<KeyCreateResult> CreateMeKeyAsync(string name, long? validForSeconds = null, CancellationToken ct = default);

    /// <summary>
    /// Deletes a personal API key by ID. Returns <c>false</c> if it did not exist.
    /// </summary>
    Task<bool> DeleteMeKeyAsync(string id, CancellationToken ct = default);

    // --- Admin: Orgs ---

    /// <summary>
    /// Creates a new organization.
    /// </summary>
    Task<OrgResponse> CreateOrgAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Lists all organizations.
    /// </summary>
    Task<IReadOnlyList<OrgResponse>> ListOrgsAsync(CancellationToken ct = default);

    /// <summary>
    /// Deletes an organization by ID. Returns <c>false</c> if it did not exist.
    /// </summary>
    Task<bool> DeleteOrgAsync(string id, CancellationToken ct = default);

    // --- Admin: Principals ---

    /// <summary>
    /// Creates a new principal (user or service account) in an organization.
    /// </summary>
    Task<PrincipalResponse> CreatePrincipalAsync(string orgId, string role, string name, CancellationToken ct = default);

    /// <summary>
    /// Lists all principals in an organization.
    /// </summary>
    Task<IReadOnlyList<PrincipalResponse>> ListPrincipalsAsync(string orgId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a principal by ID from an organization. Returns <c>false</c> if it did not exist.
    /// </summary>
    Task<bool> DeletePrincipalAsync(string orgId, string id, CancellationToken ct = default);

    // --- Admin: Roles ---

    /// <summary>
    /// Creates a new role in an organization.
    /// </summary>
    Task<RoleResponse> CreateRoleAsync(string orgId, string name, string[] permissions, CancellationToken ct = default);

    /// <summary>
    /// Lists all roles in an organization.
    /// </summary>
    Task<IReadOnlyList<RoleResponse>> ListRolesAsync(string orgId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a role by name from an organization. Returns <c>false</c> if it did not exist.
    /// </summary>
    Task<bool> DeleteRoleAsync(string orgId, string name, CancellationToken ct = default);
}
