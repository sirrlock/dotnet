using System.Net;
using System.Text.Json;
using Sirr.Tests.Helpers;

namespace Sirr.Tests;

public sealed class SirrClientTests
{
    private static (SirrClient Client, MockHttpHandler Handler) CreateClient()
    {
        var handler = new MockHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "test-token");
        var client = new SirrClient(http);
        return (client, handler);
    }

    // --- Health ---

    [Fact]
    public async Task HealthAsync_ReturnsTrue_WhenStatusOk()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { status = "ok" });

        var result = await client.HealthAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task HealthAsync_ReturnsFalse_WhenServerError()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueError(HttpStatusCode.InternalServerError, "down");

        var result = await client.HealthAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task HealthAsync_RequestsCorrectPath()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { status = "ok" });

        await client.HealthAsync();

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("/health", request.RequestUri!.AbsolutePath);
    }

    // --- Push ---

    [Fact]
    public async Task PushAsync_SendsCorrectRequest()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { key = "DB_URL" });

        await client.PushAsync("DB_URL", "postgres://...", ttl: TimeSpan.FromMinutes(30), reads: 5);

        var request = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/secrets", request.RequestUri!.AbsolutePath);

        using var doc = JsonDocument.Parse(request.Body!);
        var root = doc.RootElement;

        Assert.Equal("DB_URL", root.GetProperty("key").GetString());
        Assert.Equal("postgres://...", root.GetProperty("value").GetString());
        Assert.Equal(1800, root.GetProperty("ttl_seconds").GetInt64());
        Assert.Equal(5, root.GetProperty("max_reads").GetInt32());
    }

    [Fact]
    public async Task PushAsync_OmitsNullOptionals()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { key = "K" });

        await client.PushAsync("K", "V");

        using var doc = JsonDocument.Parse(handler.Requests[0].Body!);
        var root = doc.RootElement;

        Assert.False(root.TryGetProperty("ttl_seconds", out _));
        Assert.False(root.TryGetProperty("max_reads", out _));
    }

    [Fact]
    public async Task PushAsync_IncludesAuthHeader()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { key = "K" });

        await client.PushAsync("K", "V");

        var auth = handler.Requests[0].Authorization;
        Assert.NotNull(auth);
        Assert.Equal("Bearer", auth!.Scheme);
        Assert.Equal("test-token", auth.Parameter);
    }

    // --- Get ---

    [Fact]
    public async Task GetAsync_ReturnsValue_OnSuccess()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { key = "K", value = "secret-value" });

        var result = await client.GetAsync("K");

        Assert.Equal("secret-value", result);
        Assert.Equal("/secrets/K", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_On404()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueNotFound();

        var result = await client.GetAsync("missing");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_Throws_OnServerError()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueError(HttpStatusCode.InternalServerError, "boom");

        var ex = await Assert.ThrowsAsync<SirrException>(() => client.GetAsync("K"));

        Assert.Equal(500, ex.StatusCode);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task GetAsync_UrlEncodesKey()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { key = "my/key", value = "v" });

        await client.GetAsync("my/key");

        Assert.Equal("/secrets/my%2Fkey", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    // --- Delete ---

    [Fact]
    public async Task DeleteAsync_ReturnsTrue_OnSuccess()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { deleted = true });

        var result = await client.DeleteAsync("K");

        Assert.True(result);
        Assert.Equal(HttpMethod.Delete, handler.Requests[0].Method);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_On404()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueNotFound();

        var result = await client.DeleteAsync("missing");

        Assert.False(result);
    }

    [Fact]
    public async Task DeleteAsync_Throws_OnOtherError()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueError(HttpStatusCode.Forbidden, "denied");

        var ex = await Assert.ThrowsAsync<SirrException>(() => client.DeleteAsync("K"));

        Assert.Equal(403, ex.StatusCode);
    }

    // --- List ---

    [Fact]
    public async Task ListAsync_ReturnsSecretMetas()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new
        {
            secrets = new object[]
            {
                new { key = "A", created_at = 1000L, expires_at = 2000L, max_reads = 5, read_count = 1 },
                new { key = "B", created_at = 1100L, read_count = 0 },
            }
        });

        var result = await client.ListAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Key);
        Assert.Equal(1000L, result[0].CreatedAt);
        Assert.Equal(2000L, result[0].ExpiresAt);
        Assert.Equal(5, result[0].MaxReads);
        Assert.Equal(1, result[0].ReadCount);
        Assert.Equal("B", result[1].Key);
        Assert.Null(result[1].ExpiresAt);
        Assert.Null(result[1].MaxReads);
    }

    [Fact]
    public async Task ListAsync_HandlesEmptyList()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { secrets = Array.Empty<object>() });

        var result = await client.ListAsync();

        Assert.Empty(result);
    }

    // --- PullAll ---

    [Fact]
    public async Task PullAllAsync_CombinesListAndGet()
    {
        var (client, handler) = CreateClient();

        // List response
        handler.EnqueueOk(new
        {
            secrets = new[]
            {
                new { key = "A", created_at = 1L, read_count = 0 },
                new { key = "B", created_at = 2L, read_count = 0 },
            }
        });
        // Get A
        handler.EnqueueOk(new { key = "A", value = "val-a" });
        // Get B
        handler.EnqueueOk(new { key = "B", value = "val-b" });

        var result = await client.PullAllAsync();

        Assert.Equal(2, result.Count);
        Assert.Equal("val-a", result["A"]);
        Assert.Equal("val-b", result["B"]);
    }

    [Fact]
    public async Task PullAllAsync_SkipsBurnedSecrets()
    {
        var (client, handler) = CreateClient();

        handler.EnqueueOk(new
        {
            secrets = new[]
            {
                new { key = "A", created_at = 1L, read_count = 0 },
                new { key = "B", created_at = 2L, read_count = 0 },
            }
        });
        handler.EnqueueOk(new { key = "A", value = "val-a" });
        handler.EnqueueNotFound(); // B burned between list and get

        var result = await client.PullAllAsync();

        Assert.Single(result);
        Assert.Equal("val-a", result["A"]);
    }

    // --- Prune ---

    [Fact]
    public async Task PruneAsync_ReturnsCount()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueOk(new { pruned = 7 });

        var result = await client.PruneAsync();

        Assert.Equal(7, result);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("/prune", handler.Requests[0].RequestUri!.AbsolutePath);
    }

    // --- Error handling ---

    [Fact]
    public async Task SirrException_HasCorrectStatusCodeAndMessage()
    {
        var (client, handler) = CreateClient();
        handler.EnqueueError(HttpStatusCode.Unauthorized, "invalid token");

        var ex = await Assert.ThrowsAsync<SirrException>(() => client.ListAsync());

        Assert.Equal(401, ex.StatusCode);
        Assert.Contains("invalid token", ex.Message);
        Assert.Contains("401", ex.Message);
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_WithOptions_SetsBaseAddress()
    {
        using var client = new SirrClient(new SirrOptions
        {
            Server = "https://sirr.example.com",
            Token = "tok",
        });

        Assert.NotNull(client);
    }

    [Fact]
    public void Constructor_WithServerAndToken()
    {
        using var client = new SirrClient("https://sirr.example.com", "tok");
        Assert.NotNull(client);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_DoesNotThrow_WhenNotOwned()
    {
        var handler = new MockHttpHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8080") };
        var client = new SirrClient(http);

        client.Dispose();

        // HttpClient should still be usable since SirrClient doesn't own it
        Assert.Equal(new Uri("http://localhost:8080"), http.BaseAddress);
    }
}
