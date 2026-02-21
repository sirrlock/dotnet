using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
    public SirrClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
        _ownsHttpClient = false;
    }

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

    /// <inheritdoc />
    public async Task PushAsync(string key, string value, TimeSpan? ttl = null, int? reads = null, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentNullException.ThrowIfNull(value);

        var payload = new CreateSecretRequest
        {
            Key = key,
            Value = value,
            TtlSeconds = ttl.HasValue ? (long)ttl.Value.TotalSeconds : null,
            MaxReads = reads,
        };

        await SendAsync<CreateSecretResponse>(HttpMethod.Post, "/secrets", payload, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        try
        {
            var response = await SendAsync<GetSecretResponse>(
                HttpMethod.Get,
                $"/secrets/{Uri.EscapeDataString(key)}",
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
                $"/secrets/{Uri.EscapeDataString(key)}",
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
        var response = await SendAsync<ListSecretsResponse>(HttpMethod.Get, "/secrets", content: null, ct)
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
        var response = await SendAsync<PruneResponse>(HttpMethod.Post, "/prune", content: null, ct)
            .ConfigureAwait(false);

        return response.Pruned;
    }

    /// <inheritdoc />
    public async Task<EnvScope> CreateEnvScopeAsync(CancellationToken ct = default)
    {
        var secrets = await PullAllAsync(ct).ConfigureAwait(false);
        return new EnvScope(secrets);
    }

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
            var errorBody = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions, ct)
                .ConfigureAwait(false);

            throw new SirrException(
                (int)response.StatusCode,
                errorBody?.Error ?? response.ReasonPhrase ?? "Unknown error");
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct).ConfigureAwait(false);
        return result ?? throw new SirrException((int)response.StatusCode, "Empty response body");
    }
}
