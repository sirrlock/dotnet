// dotnet/test/Sirr.Client.Tests/IntegrationTests.cs
// Real integration tests against a live sirrd process.
// Requires SIRR_INTEGRATION=1 env var and sirrd on PATH.
// Run with: SIRR_INTEGRATION=1 dotnet test
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Sirr;
using Sirr.Tests.Helpers;
using Xunit;

namespace Sirr.Tests;

[Collection("Integration")]
public class IntegrationTests : IClassFixture<SirrdFixture>
{
    private readonly SirrdFixture _fix;
    public IntegrationTests(SirrdFixture fix) => _fix = fix;

    [Fact]
    public async Task Debug_BootstrapKeyWorks()
    {
        if (!SirrdFixture.Enabled) return;
        Assert.NotEmpty(_fix.OrgId);
        Assert.NotEmpty(_fix.BootstrapKey);
        // Verify the bootstrap key is accepted by the server directly
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _fix.BootstrapKey);
        var r = await http.GetAsync($"{SirrdFixture.Base}/me");
        var body = await r.Content.ReadAsStringAsync();
        Assert.True(r.IsSuccessStatusCode,
            $"GET /me with bootstrap key failed: {r.StatusCode} — {body}" +
            $"\nOrgId={_fix.OrgId}" +
            $"\nBootstrapKey={_fix.BootstrapKey}" +
            $"\nFixtureLogSample={_fix.LogSample}");
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        if (!SirrdFixture.Enabled) return;
        using var http = new HttpClient();
        var r = await http.GetAsync($"{SirrdFixture.Base}/health");
        Assert.True(r.IsSuccessStatusCode);
    }

    [Fact]
    public async Task PublicSecret_AdminCanPushAndGet()
    {
        if (!SirrdFixture.Enabled) return;
        // Public push returns an ID; retrieve with that ID
        var pushed = await _fix.AdminClient.PushAsync("hello-public",
            ttl: TimeSpan.FromHours(1));
        Assert.NotEmpty(pushed.Id);
        Assert.Equal("hello-public", await _fix.AdminClient.GetAsync(pushed.Id));
    }

    [Fact]
    public async Task OrgSecret_BootstrapKeyCanRead()
    {
        if (!SirrdFixture.Enabled) return;
        // OrgAdminClient uses the auto-init bootstrap key (admin principal of bootstrapped org)
        await _fix.OrgAdminClient.SetAsync("DOTNET_PRIVATE", "secret123",
            ttl: TimeSpan.FromHours(1), reads: 10);
        Assert.Equal("secret123", await _fix.OrgAdminClient.GetAsync("DOTNET_PRIVATE"));
    }

    [Fact]
    public async Task OrgSecret_NoAuthReturns401()
    {
        if (!SirrdFixture.Enabled) return;
        using var http = new HttpClient();
        var r = await http.GetAsync(
            $"{SirrdFixture.Base}/orgs/{_fix.OrgId}/secrets/DOTNET_PRIVATE");
        Assert.Equal(401, (int)r.StatusCode);
    }

    [Fact]
    public async Task OrgSecret_WrongKeyReturns401()
    {
        if (!SirrdFixture.Enabled) return;
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "definitely-wrong-key");
        var r = await http.GetAsync(
            $"{SirrdFixture.Base}/orgs/{_fix.OrgId}/secrets/DOTNET_PRIVATE");
        Assert.Equal(401, (int)r.StatusCode);
    }

    [Fact]
    public async Task BurnAfterRead()
    {
        if (!SirrdFixture.Enabled) return;
        await _fix.OrgAdminClient.SetAsync("DOTNET_BURN", "burnme",
            ttl: TimeSpan.FromHours(1), reads: 1);
        Assert.Equal("burnme", await _fix.OrgAdminClient.GetAsync("DOTNET_BURN"));
        Assert.Null(await _fix.OrgAdminClient.GetAsync("DOTNET_BURN"));
    }
}
