// ExternalServerTests.cs
// End-to-end tests against an externally-running sirrd server.
// Requires SIRR_EXTERNAL=1 env var to run.
// Example:
//   SIRR_EXTERNAL=1 \
//   SIRR_SERVER="http://127.0.0.1:39999" \
//   SIRR_API_KEY="sirr_key_..." \
//   dotnet test --filter "ExternalServer"
using System;
using System.Linq;
using System.Threading.Tasks;
using Sirr;
using Xunit;
using Xunit.Abstractions;

namespace Sirr.Tests;

public class ExternalServerTests(ITestOutputHelper output)
{
    private static readonly bool Enabled =
        Environment.GetEnvironmentVariable("SIRR_EXTERNAL") == "1";

    private static readonly string Server =
        Environment.GetEnvironmentVariable("SIRR_SERVER") ?? "http://127.0.0.1:39999";

    private static readonly string ApiKey =
        Environment.GetEnvironmentVariable("SIRR_API_KEY") ?? "";

    private static SirrClient MasterClient() => new(new SirrOptions
    {
        Server = Server,
        Token  = ApiKey,
    });

    // --- Health ---

    [Fact]
    public async Task Health_ReturnsOk()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        var ok = await client.HealthAsync();
        Assert.True(ok, "GET /health did not return ok");
    }

    // --- README example: connect, push, get, delete ---

    [Fact]
    public async Task ReadmeExample_ConnectsAndPushes()
    {
        if (!Enabled) return;

        using var sirr = new SirrClient(new SirrOptions
        {
            Server = Server,
            Token  = ApiKey,
        });

        const string key = "EXT_README_KEY";
        // sealOnExpiry: true so we can read twice without burning it on first read
        await sirr.PushAsync(key, "readme-value", ttl: TimeSpan.FromMinutes(5), reads: 2, sealOnExpiry: true);

        var first = await sirr.GetAsync(key);
        Assert.Equal("readme-value", first);

        await sirr.DeleteAsync(key);
        output.WriteLine("ReadmeExample: push/get/delete round-trip passed");
    }

    // --- Public (unscoped) secrets: push / get / patch / delete ---

    [Fact]
    public async Task Public_PushGetPatchDelete()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        const string key = "EXT_KEY";

        // Push with sealOnExpiry=true so PATCH is allowed
        await client.PushAsync(key, "hello", ttl: TimeSpan.FromHours(1), reads: 10, sealOnExpiry: true);
        output.WriteLine($"PushAsync (seal mode): {key}");

        var value = await client.GetAsync(key);
        Assert.Equal("hello", value);
        output.WriteLine($"GetAsync: {value}");

        await client.PatchAsync(key, ttl: TimeSpan.FromHours(2));
        output.WriteLine("PatchAsync: extended TTL");

        var deleted = await client.DeleteAsync(key);
        Assert.True(deleted);
        output.WriteLine($"DeleteAsync: {deleted}");

        var afterDelete = await client.GetAsync(key);
        Assert.Null(afterDelete);
    }

    // --- Burn-after-read ---

    [Fact]
    public async Task Public_BurnAfterRead()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        const string key = "EXT_BURN";

        await client.PushAsync(key, "burn", reads: 1, ttl: TimeSpan.FromMinutes(5));

        var first = await client.GetAsync(key);
        Assert.Equal("burn", first);

        var second = await client.GetAsync(key);
        Assert.Null(second);
        output.WriteLine("BurnAfterRead: second read correctly returned null");
    }

    // --- HEAD: check without consuming a read ---

    [Fact]
    public async Task Public_HeadDoesNotConsumeRead()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        const string key = "EXT_HEAD";

        await client.PushAsync(key, "head-value", reads: 1, ttl: TimeSpan.FromMinutes(5));

        // HEAD should not consume the read
        var status = await client.HeadAsync(key);
        Assert.NotNull(status);
        Assert.False(status!.IsSealed);
        Assert.Equal(0, status.ReadCount);
        output.WriteLine($"HeadAsync: read_count={status.ReadCount} reads_remaining={status.ReadsRemaining}");

        // GET should still succeed (read counter was not spent by HEAD)
        var value = await client.GetAsync(key);
        Assert.Equal("head-value", value);
        output.WriteLine("GetAsync after HeadAsync: still returned value (read not consumed by HEAD)");

        // Second GET should return null (budget exhausted)
        var second = await client.GetAsync(key);
        Assert.Null(second);
        output.WriteLine("Second GetAsync: null (budget exhausted as expected)");
    }

    // --- Admin: Org lifecycle ---

    [Fact]
    public async Task Admin_CreateAndDeleteOrg()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        const string orgName = "dotnet-test-org";
        OrgResponse? org = null;

        try
        {
            org = await client.CreateOrgAsync(orgName);
            output.WriteLine($"CreateOrgAsync: id={org.Id} name={org.Name}");
            Assert.Equal(orgName, org.Name);
            Assert.NotEmpty(org.Id);

            var orgs = await client.ListOrgsAsync();
            Assert.Contains(orgs, o => o.Id == org.Id);
            output.WriteLine($"ListOrgsAsync: found {orgName}");
        }
        finally
        {
            if (org is not null)
            {
                var del = await client.DeleteOrgAsync(org.Id);
                output.WriteLine($"DeleteOrgAsync({org.Id}): {del}");
            }
        }
    }

    // --- Admin: Role lifecycle ---

    [Fact]
    public async Task Admin_CreateAndDeleteRole()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        OrgResponse? org = null;

        try
        {
            org = await client.CreateOrgAsync("dotnet-role-test-org");
            output.WriteLine($"Created org: {org.Id}");

            var role = await client.CreateRoleAsync(org.Id, "dotnet-viewer", "R");
            output.WriteLine($"CreateRoleAsync: name={role.Name} permissions={role.Permissions} org_id={role.OrgId}");
            Assert.Equal("dotnet-viewer", role.Name);
            Assert.Equal("R", role.Permissions);
            Assert.Equal(org.Id, role.OrgId);

            var roles = await client.ListRolesAsync(org.Id);
            Assert.Contains(roles, r => r.Name == "dotnet-viewer");
            output.WriteLine($"ListRolesAsync: found dotnet-viewer");

            var deleted = await client.DeleteRoleAsync(org.Id, "dotnet-viewer");
            Assert.True(deleted);
            output.WriteLine($"DeleteRoleAsync: {deleted}");
        }
        finally
        {
            if (org is not null)
            {
                var del = await client.DeleteOrgAsync(org.Id);
                output.WriteLine($"DeleteOrgAsync({org.Id}): {del}");
            }
        }
    }

    // --- Admin: Principal lifecycle ---

    [Fact]
    public async Task Admin_CreateAndDeletePrincipal()
    {
        if (!Enabled) return;

        using var client = MasterClient();
        PrincipalResponse? principal = null;
        OrgResponse? org = null;

        try
        {
            org = await client.CreateOrgAsync("dotnet-principal-test-org");
            output.WriteLine($"Created org: {org.Id}");

            // Role "writer" is a built-in role in sirrd
            principal = await client.CreatePrincipalAsync(org.Id, "writer", "dotnet-test-user");
            output.WriteLine($"CreatePrincipalAsync: id={principal.Id} role={principal.Role} name={principal.Name}");

            Assert.NotEmpty(principal.Id);
            Assert.Equal("writer", principal.Role);

            var principals = await client.ListPrincipalsAsync(org.Id);
            Assert.Contains(principals, p => p.Id == principal.Id);
            output.WriteLine($"ListPrincipalsAsync: found {principal.Id}");
        }
        finally
        {
            if (principal is not null)
            {
                var del = await client.DeletePrincipalAsync(org!.Id, principal.Id);
                output.WriteLine($"DeletePrincipalAsync: {del}");
            }
            if (org is not null)
            {
                var del = await client.DeleteOrgAsync(org.Id);
                output.WriteLine($"DeleteOrgAsync({org.Id}): {del}");
            }
        }
    }
}
