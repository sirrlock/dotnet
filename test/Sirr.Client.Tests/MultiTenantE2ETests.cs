// dotnet/test/Sirr.Client.Tests/MultiTenantE2ETests.cs
// Multi-tenant E2E: two companies on one server, full isolation.
// NO license key — tests the real free-tier experience.
// Run with: SIRR_INTEGRATION=1 dotnet test --filter "FullyQualifiedName~MultiTenant"
using System.Diagnostics;
using System.Net.Http.Json;
using Sirr;
using Xunit;

namespace Sirr.Tests;

public class MultiTenantFixture : IAsyncLifetime
{
    public static readonly bool Enabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIRR_INTEGRATION"));

    public const string MasterKey = "dotnet-mt-e2e-master-key";
    public const int Port = 39994;
    public static string Base => $"http://localhost:{Port}";

    private Process? _sirrd;
    private string? _dataDir;

    public SirrClient Master { get; private set; } = null!;

    public string AcmeId { get; private set; } = null!;
    public string GlobexId { get; private set; } = null!;

    public string AliceKey { get; private set; } = null!;
    public string BobKey { get; private set; } = null!;
    public string CarolKey { get; private set; } = null!;
    public string HankKey { get; private set; } = null!;
    public string MargeKey { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        if (!Enabled) return;

        _dataDir = Path.Combine(Path.GetTempPath(), $"sirr-mt-dotnet-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);

        var sirrdBin = Environment.GetEnvironmentVariable("SIRRD_BIN") ?? "sirrd";
        var psi = new ProcessStartInfo
        {
            FileName = sirrdBin,
            Arguments = $"serve --port {Port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.Environment["SIRR_MASTER_API_KEY"] = MasterKey;
        psi.Environment["SIRR_DATA_DIR"] = _dataDir;
        psi.Environment["SIRR_RATE_LIMIT_PER_SECOND"] = "1000";
        psi.Environment["SIRR_RATE_LIMIT_BURST"] = "1000";

        _sirrd = Process.Start(psi)!;
        _sirrd.BeginOutputReadLine();
        _sirrd.BeginErrorReadLine();

        await WaitForHealthAsync();

        Master = new SirrClient(new SirrOptions { Server = Base, Token = MasterKey });

        // ── Acme: org + 3 principals + keys ─────────────────────────────
        var acme = await Master.CreateOrgAsync("acme");
        AcmeId = acme.Id;

        var alice = await Master.CreatePrincipalAsync(AcmeId, "owner", "alice");
        var bob = await Master.CreatePrincipalAsync(AcmeId, "writer", "bob");
        var carol = await Master.CreatePrincipalAsync(AcmeId, "reader", "carol");

        var aliceKeyResult = await Master.CreatePrincipalKeyAsync(AcmeId, alice.Id, "alice-key");
        var bobKeyResult = await Master.CreatePrincipalKeyAsync(AcmeId, bob.Id, "bob-key");
        var carolKeyResult = await Master.CreatePrincipalKeyAsync(AcmeId, carol.Id, "carol-key");

        AliceKey = aliceKeyResult.Key;
        BobKey = bobKeyResult.Key;
        CarolKey = carolKeyResult.Key;

        // ── Globex: org + 2 principals + keys ───────────────────────────
        var globex = await Master.CreateOrgAsync("globex");
        GlobexId = globex.Id;

        var hank = await Master.CreatePrincipalAsync(GlobexId, "owner", "hank");
        var marge = await Master.CreatePrincipalAsync(GlobexId, "writer", "marge");

        var hankKeyResult = await Master.CreatePrincipalKeyAsync(GlobexId, hank.Id, "hank-key");
        var margeKeyResult = await Master.CreatePrincipalKeyAsync(GlobexId, marge.Id, "marge-key");

        HankKey = hankKeyResult.Key;
        MargeKey = margeKeyResult.Key;
    }

    public Task DisposeAsync()
    {
        Master?.Dispose();
        _sirrd?.Kill(entireProcessTree: true);
        if (_dataDir != null)
        {
            try { Directory.Delete(_dataDir, recursive: true); } catch { }
        }
        return Task.CompletedTask;
    }

    public SirrClient AcmeAs(string token) =>
        new(new SirrOptions { Server = Base, Token = token, Org = AcmeId });

    public SirrClient GlobexAs(string token) =>
        new(new SirrOptions { Server = Base, Token = token, Org = GlobexId });

    private static async Task WaitForHealthAsync()
    {
        using var http = new HttpClient();
        for (int i = 0; i < 30; i++)
        {
            try
            {
                var r = await http.GetAsync($"{Base}/health");
                if (r.IsSuccessStatusCode) return;
            }
            catch { }
            await Task.Delay(200);
        }
        throw new InvalidOperationException("sirrd did not start in time");
    }
}

[CollectionDefinition("MultiTenant")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix")]
public class MultiTenantCollection : ICollectionFixture<MultiTenantFixture> { }

[Collection("MultiTenant")]
public class MultiTenantSetupTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantSetupTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task ServerHealthy()
    {
        if (!MultiTenantFixture.Enabled) return;
        Assert.True(await _f.Master.HealthAsync());
    }

    [Fact]
    public async Task TwoOrgsCreated()
    {
        if (!MultiTenantFixture.Enabled) return;
        var orgs = await _f.Master.ListOrgsAsync();
        var names = orgs.Select(o => o.Name).ToList();
        Assert.Contains("acme", names);
        Assert.Contains("globex", names);
    }
}

[Collection("MultiTenant")]
public class MultiTenantAuthTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantAuthTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task AliceAuthenticates()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.AliceKey);
        var me = await c.GetMeAsync();
        Assert.Equal("alice", me.Name);
        Assert.Equal("owner", me.Role);
    }

    [Fact]
    public async Task BobAuthenticates()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.BobKey);
        var me = await c.GetMeAsync();
        Assert.Equal("bob", me.Name);
        Assert.Equal("writer", me.Role);
    }

    [Fact]
    public async Task HankAuthenticates()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.GlobexAs(_f.HankKey);
        var me = await c.GetMeAsync();
        Assert.Equal("hank", me.Name);
        Assert.Equal("owner", me.Role);
    }
}

[Collection("MultiTenant")]
public class MultiTenantAcmeSecretTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantAcmeSecretTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task AliceOwnerSetAndRead()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.AliceKey);
        await c.SetAsync("DB_URL", "postgres://acme-db:5432/acme", reads: 10);
        Assert.Equal("postgres://acme-db:5432/acme", await c.GetAsync("DB_URL"));
    }

    [Fact]
    public async Task BobWriterSetAndRead()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.BobKey);
        await c.SetAsync("API_KEY", "acme-api-key-42", reads: 10);
        Assert.Equal("acme-api-key-42", await c.GetAsync("API_KEY"));
    }

    [Fact]
    public async Task CarolReaderCannotCreate()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.CarolKey);
        var ex = await Assert.ThrowsAsync<SirrException>(() => c.SetAsync("NOPE", "denied"));
        Assert.Equal(403, ex.StatusCode);
    }
}

[Collection("MultiTenant")]
public class MultiTenantGlobexSecretTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantGlobexSecretTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task HankSetsDbUrlSameKeyName()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.GlobexAs(_f.HankKey);
        await c.SetAsync("DB_URL", "postgres://globex-db:5432/globex", reads: 10);
        Assert.Equal("postgres://globex-db:5432/globex", await c.GetAsync("DB_URL"));
    }

    [Fact]
    public async Task MargeWriterSetAndRead()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.GlobexAs(_f.MargeKey);
        await c.SetAsync("STRIPE_KEY", "sk_test_globex", reads: 10);
        Assert.Equal("sk_test_globex", await c.GetAsync("STRIPE_KEY"));
    }
}

[Collection("MultiTenant")]
public class MultiTenantIsolationTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantIsolationTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task AcmeDbUrlStillAcme()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.AliceKey);
        Assert.Equal("postgres://acme-db:5432/acme", await c.GetAsync("DB_URL"));
    }

    [Fact]
    public async Task GlobexDbUrlStillGlobex()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.GlobexAs(_f.HankKey);
        Assert.Equal("postgres://globex-db:5432/globex", await c.GetAsync("DB_URL"));
    }
}

[Collection("MultiTenant")]
public class MultiTenantCrossOrgTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantCrossOrgTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task HankCannotReadAcme()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.HankKey);
        await Assert.ThrowsAsync<SirrException>(() => c.GetAsync("DB_URL"));
    }

    [Fact]
    public async Task AliceCannotReadGlobex()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.GlobexAs(_f.AliceKey);
        await Assert.ThrowsAsync<SirrException>(() => c.GetAsync("DB_URL"));
    }

    [Fact]
    public async Task MargeCannotWriteAcme()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.MargeKey);
        await Assert.ThrowsAsync<SirrException>(() => c.SetAsync("HACK", "nope"));
    }

    [Fact]
    public async Task BobCannotWriteGlobex()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.GlobexAs(_f.BobKey);
        await Assert.ThrowsAsync<SirrException>(() => c.SetAsync("HACK", "nope"));
    }
}

[Collection("MultiTenant")]
public class MultiTenantPublicDeadDropTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantPublicDeadDropTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task PushAndGetWithoutOrg()
    {
        if (!MultiTenantFixture.Enabled) return;
        // Use raw HttpClient — SirrClient requires org for get routing
        using var http = new HttpClient { BaseAddress = new Uri(MultiTenantFixture.Base) };
        var pushResp = await http.PostAsJsonAsync("/secrets", new { value = "hello-from-dotnet" });
        pushResp.EnsureSuccessStatusCode();
        var pushBody = await pushResp.Content.ReadFromJsonAsync<PushIdResponse>();
        Assert.NotNull(pushBody?.Id);

        using var anon = new SirrClient(new SirrOptions { Server = MultiTenantFixture.Base, Token = "unused" });
        var value = await anon.GetAsync(pushBody!.Id);
        Assert.Equal("hello-from-dotnet", value);
    }

    private sealed record PushIdResponse
    {
        public string Id { get; init; } = "";
    }
}

[Collection("MultiTenant")]
public class MultiTenantBurnAfterReadTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantBurnAfterReadTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task FirstReadReturnsSecondReturnsNull()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.AliceKey);
        await c.SetAsync("BURN_DOTNET", "burnme", reads: 1);
        Assert.Equal("burnme", await c.GetAsync("BURN_DOTNET"));
        Assert.Null(await c.GetAsync("BURN_DOTNET"));
    }
}

[Collection("MultiTenant")]
public class MultiTenantSelfServiceKeyTests
{
    private readonly MultiTenantFixture _f;
    public MultiTenantSelfServiceKeyTests(MultiTenantFixture f) => _f = f;

    [Fact]
    public async Task AliceCreatesOwnKey()
    {
        if (!MultiTenantFixture.Enabled) return;
        using var c = _f.AcmeAs(_f.AliceKey);
        var newKey = await c.CreateMeKeyAsync("alice-self-key");
        Assert.NotEmpty(newKey.Key);

        using var c2 = _f.AcmeAs(newKey.Key);
        var me = await c2.GetMeAsync();
        Assert.Equal("alice", me.Name);
    }
}
