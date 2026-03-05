using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Sirr.Internal;

namespace Sirr;

/// <summary>
/// HTTP client for the Sirr ephemeral secrets API.
/// </summary>
public sealed class SirrClient : ISirrClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly string? _org;

    /// <summary>
    /// Creates a client with the given options. Owns and disposes the underlying HttpClient.
    /// </summary>
    public SirrClient(SirrOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = new HttpClient
        {
            BaseAddress = new Uri(options.Server.TrimEnd('/')),
        };
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", options.Token);
        _ownsHttpClient = true;
        _org = options.Org;
    }

    /// <summary>
    /// Creates a client with server URL and token. Owns and disposes the underlying HttpClient.
    /// </summary>
    public SirrClient(string server, string token)
        : this(new SirrOptions { Server = server, Token = token })
    {
    }

    /// <summary>
    /// Creates a client using an externally-managed HttpClient (e.g. from IHttpClientFactory).
    /// The caller is responsible for HttpClient lifetime.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public SirrClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
        _ownsHttpClient = false;
    }

    /// <summary>
    /// Creates a client using an externally-managed HttpClient with org-scoping.
    /// The caller is responsible for HttpClient lifetime.
    /// </summary>
    public SirrClient(HttpClient httpClient, string? org)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
        _ownsHttpClient = false;
        _org = org;
    }

    // --- Path helpers ---

    private string OrgPrefix => _org is not null ? $"/orgs/{Uri.EscapeDataString(_org)}" : "";

    private string SecretsPath(string? key = null) =>
        key is not null
            ? $"{OrgPrefix}/secrets/{Uri.EscapeDataString(key)}"
            : $"{OrgPrefix}/secrets";

    private string AuditPath() => $"{OrgPrefix}/audit";

    private string WebhooksPath(string? id = null) =>
        id is not null
            ? $"{OrgPrefix}/webhooks/{Uri.EscapeDataString(id)}"
            : $"{OrgPrefix}/webhooks";

    private string PrunePath() => $"{OrgPrefix}/prune";

    // --- Health ---

    /// <inheritdoc />
    public async Task<bool> HealthAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return false;
        }

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(JsonOptions, ct).ConfigureAwait(false);
        return body?.Status == "ok";
    }

    // --- Secrets ---

    /// <inheritdoc />
    public async Task PushAsync(string key, string value, TimeSpan? ttl = null, int? reads = null, bool? sealOnExpiry = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var payload = new CreateSecretRequest
        {
            Key = key,
            Value = value,
            TtlSeconds = ttl.HasValue ? (long)ttl.Value.TotalSeconds : null,
            MaxReads = reads,
            // sealOnExpiry=true → delete=false (seal mode, PATCH allowed)
            // sealOnExpiry=false/null → delete=true (burn-after-read, server default)
            Delete = sealOnExpiry.HasValue ? !sealOnExpiry.Value : null,
        };

        await SendAsync<CreateSecretResponse>(HttpMethod.Post, SecretsPath(), payload, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task PatchAsync(string key, TimeSpan? ttl = null, int? reads = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        var payload = new PatchSecretRequest
        {
            TtlSeconds = ttl.HasValue ? (long)ttl.Value.TotalSeconds : null,
            MaxReads = reads,
        };

        await SendAsync<PatchSecretResponse>(HttpMethod.Patch, SecretsPath(key), payload, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            var response = await SendAsync<GetSecretResponse>(
                HttpMethod.Get,
                SecretsPath(key),
                content: null,
                ct).ConfigureAwait(false);

            return response.Value;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            await SendAsync<DeleteSecretResponse>(
                HttpMethod.Delete,
                SecretsPath(key),
                content: null,
                ct).ConfigureAwait(false);

            return true;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SecretMeta>> ListAsync(CancellationToken ct = default)
    {
        var response = await SendAsync<ListSecretsResponse>(HttpMethod.Get, SecretsPath(), content: null, ct)
            .ConfigureAwait(false);

        return response.Secrets;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> PullAllAsync(CancellationToken ct = default)
    {
        var metas = await ListAsync(ct).ConfigureAwait(false);
        var result = new Dictionary<string, string>(metas.Count);

        foreach (var meta in metas)
        {
            var value = await GetAsync(meta.Key, ct).ConfigureAwait(false);
            if (value is not null)
            {
                result[meta.Key] = value;
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<int> PruneAsync(CancellationToken ct = default)
    {
        var response = await SendAsync<PruneResponse>(HttpMethod.Post, PrunePath(), content: null, ct)
            .ConfigureAwait(false);

        return response.Pruned;
    }

    /// <inheritdoc />
    public async Task<EnvScope> CreateEnvScopeAsync(CancellationToken ct = default)
    {
        var secrets = await PullAllAsync(ct).ConfigureAwait(false);
        return new EnvScope(secrets);
    }

    // --- Audit ---

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEvent>> GetAuditLogAsync(long? since = null, long? until = null, string? action = null, int? limit = null, CancellationToken ct = default)
    {
        var queryParts = new List<string>();
        if (since.HasValue) queryParts.Add($"since={since.Value}");
        if (until.HasValue) queryParts.Add($"until={until.Value}");
        if (action is not null) queryParts.Add($"action={Uri.EscapeDataString(action)}");
        if (limit.HasValue) queryParts.Add($"limit={limit.Value}");
        var qs = queryParts.Count > 0 ? "?" + string.Join("&", queryParts) : "";

        var response = await SendAsync<AuditEventsResponse>(HttpMethod.Get, $"{AuditPath()}{qs}", content: null, ct)
            .ConfigureAwait(false);
        return response.Events;
    }

    // --- Webhooks ---

    /// <inheritdoc />
    public async Task<WebhookCreateResult> CreateWebhookAsync(string url, string[]? events = null, CancellationToken ct = default)
    {
        var payload = new CreateWebhookRequest { Url = url, Events = events };
        return await SendAsync<WebhookCreateResult>(HttpMethod.Post, WebhooksPath(), payload, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Webhook>> ListWebhooksAsync(CancellationToken ct = default)
    {
        var response = await SendAsync<ListWebhooksResponse>(HttpMethod.Get, WebhooksPath(), content: null, ct)
            .ConfigureAwait(false);
        return response.Webhooks;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteWebhookAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await SendAsync<DeletedResponse>(HttpMethod.Delete, WebhooksPath(id), content: null, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // --- /me ---

    /// <inheritdoc />
    public async Task<MeResponse> GetMeAsync(CancellationToken ct = default)
    {
        return await SendAsync<MeResponse>(HttpMethod.Get, "/me", content: null, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<MeResponse> UpdateMeAsync(string? name = null, string? email = null, CancellationToken ct = default)
    {
        var payload = new UpdateMeRequest { Name = name, Email = email };
        return await SendAsync<MeResponse>(HttpMethod.Patch, "/me", payload, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<KeyCreateResult> CreateMeKeyAsync(string name, long? validForSeconds = null, CancellationToken ct = default)
    {
        var payload = new CreateMeKeyRequest { Name = name, ValidForSeconds = validForSeconds };
        return await SendAsync<KeyCreateResult>(HttpMethod.Post, "/me/keys", payload, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteMeKeyAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await SendAsync<DeletedResponse>(HttpMethod.Delete, $"/me/keys/{Uri.EscapeDataString(id)}", content: null, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // --- Admin: Orgs ---

    /// <inheritdoc />
    public async Task<OrgResponse> CreateOrgAsync(string name, CancellationToken ct = default)
    {
        var payload = new CreateOrgRequest { Name = name };
        return await SendAsync<OrgResponse>(HttpMethod.Post, "/orgs", payload, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OrgResponse>> ListOrgsAsync(CancellationToken ct = default)
    {
        var response = await SendAsync<ListOrgsResponse>(HttpMethod.Get, "/orgs", content: null, ct)
            .ConfigureAwait(false);
        return response.Orgs;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteOrgAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await SendAsync<DeletedResponse>(HttpMethod.Delete, $"/orgs/{Uri.EscapeDataString(id)}", content: null, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // --- Admin: Principals ---

    /// <inheritdoc />
    public async Task<PrincipalResponse> CreatePrincipalAsync(string orgId, string role, string name, CancellationToken ct = default)
    {
        var payload = new CreatePrincipalRequest { Role = role, Name = name };
        return await SendAsync<PrincipalResponse>(HttpMethod.Post,
            $"/orgs/{Uri.EscapeDataString(orgId)}/principals", payload, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PrincipalResponse>> ListPrincipalsAsync(string orgId, CancellationToken ct = default)
    {
        var response = await SendAsync<ListPrincipalsResponse>(HttpMethod.Get,
            $"/orgs/{Uri.EscapeDataString(orgId)}/principals", content: null, ct)
            .ConfigureAwait(false);
        return response.Principals;
    }

    /// <inheritdoc />
    public async Task<bool> DeletePrincipalAsync(string orgId, string id, CancellationToken ct = default)
    {
        try
        {
            await SendAsync<DeletedResponse>(HttpMethod.Delete,
                $"/orgs/{Uri.EscapeDataString(orgId)}/principals/{Uri.EscapeDataString(id)}", content: null, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // --- Admin: Roles ---

    /// <inheritdoc />
    public async Task<RoleResponse> CreateRoleAsync(string orgId, string name, string[] permissions, CancellationToken ct = default)
    {
        var payload = new CreateRoleRequest { Name = name, Permissions = permissions };
        return await SendAsync<RoleResponse>(HttpMethod.Post,
            $"/orgs/{Uri.EscapeDataString(orgId)}/roles", payload, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(string orgId, CancellationToken ct = default)
    {
        var response = await SendAsync<ListRolesResponse>(HttpMethod.Get,
            $"/orgs/{Uri.EscapeDataString(orgId)}/roles", content: null, ct)
            .ConfigureAwait(false);
        return response.Roles;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteRoleAsync(string orgId, string name, CancellationToken ct = default)
    {
        try
        {
            await SendAsync<DeletedResponse>(HttpMethod.Delete,
                $"/orgs/{Uri.EscapeDataString(orgId)}/roles/{Uri.EscapeDataString(name)}", content: null, ct)
                .ConfigureAwait(false);
            return true;
        }
        catch (SirrException ex) when (ex.StatusCode == (int)HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // --- Dispose ---

    /// <summary>
    /// Disposes the underlying HttpClient if this instance owns it.
    /// </summary>
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    // --- Internal ---

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, path);

        if (content is not null)
        {
            request.Content = JsonContent.Create(content, options: JsonOptions);
        }

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? errorMessage = null;
            try
            {
                var errorBody = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct)
                    .ConfigureAwait(false);
                errorMessage = errorBody?.Error;
            }
            catch (JsonException)
            {
                // Non-JSON error body (e.g. rate-limiter plain-text responses)
                errorMessage = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            }

            throw new SirrException(
                (int)response.StatusCode,
                errorMessage ?? response.ReasonPhrase ?? "Unknown error");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        return result ?? throw new SirrException((int)response.StatusCode, "Empty response body");
    }
}
