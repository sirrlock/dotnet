// dotnet/test/Sirr.Client.Tests/Helpers/SirrdFixture.cs
// Starts and stops a real sirrd process for integration tests.
// Uses SIRR_AUTOINIT=1 to bootstrap a default org + admin principal + temp keys.
// Tests are skipped if SIRR_INTEGRATION env var is not set.
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sirr;
using Xunit;

namespace Sirr.Tests.Helpers;

public class SirrdFixture : IAsyncLifetime
{
    public static readonly bool Enabled =
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SIRR_INTEGRATION"));

    public const string MasterKey = "dotnet-integration-test-key";
    private const string LicenseKey = "sirr_lic_0000000000000000000000000000000000000000";
    public static readonly int Port = 39995;
    public static string Base => $"http://localhost:{Port}";

    private Process? _sirrd;
    private string? _dataDir;
    private readonly ConcurrentQueue<string> _logLines = new();

    public SirrClient AdminClient { get; private set; } = null!;
    public string OrgId { get; private set; } = null!;
    public string BootstrapKey { get; private set; } = null!;
    public SirrClient OrgAdminClient { get; private set; } = null!;
    public string LogSample { get; private set; } = "(not captured)";

    public async Task InitializeAsync()
    {
        if (!Enabled) return;

        _dataDir = Path.Combine(Path.GetTempPath(), $"sirr-e2e-dotnet-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);

        var psi = new ProcessStartInfo
        {
            FileName = "sirrd",
            Arguments = $"serve --port {Port}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // Inherit current environment and add test overrides
        psi.Environment["SIRR_API_KEY"] = MasterKey;
        psi.Environment["SIRR_LICENSE_KEY"] = LicenseKey;
        psi.Environment["SIRR_AUTOINIT"] = "1";
        psi.Environment["SIRR_DATA_DIR"] = _dataDir;

        // Kill any stale sirrd on the port so our process can bind
        KillPortStalkers(Port);
        await Task.Delay(300); // Let the OS release the port

        _sirrd = Process.Start(psi)!;

        // Collect output asynchronously to parse bootstrap info
        _sirrd.OutputDataReceived += (_, e) => { if (e.Data != null) _logLines.Enqueue(e.Data); };
        _sirrd.ErrorDataReceived += (_, e) => { if (e.Data != null) _logLines.Enqueue(e.Data); };
        _sirrd.BeginOutputReadLine();
        _sirrd.BeginErrorReadLine();

        await WaitForHealthAsync();

        // Verify our process is still alive — if it exited, it failed to bind the port
        if (_sirrd.HasExited)
        {
            var earlyLog = string.Join("\n", _logLines);
            throw new InvalidOperationException(
                $"sirrd exited immediately after start (port {Port} bind failure?). Log:\n{earlyLog}");
        }

        // Poll until the bootstrap key appears in the log output
        var log = await WaitForBootstrapKeyAsync();
        LogSample = log.Length > 500 ? log[..500] : log;
        ParseBootstrapInfo(log);

        AdminClient = new SirrClient(new SirrOptions { Server = Base, Token = MasterKey });
        OrgAdminClient = new SirrClient(new SirrOptions { Server = Base, Token = BootstrapKey, Org = OrgId });

        // Create alice + bob in the bootstrapped org (master key for admin ops)
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", MasterKey);
        await http.PostAsJsonAsync($"{Base}/orgs/{OrgId}/principals",
            new { name = "alice", role = "writer" });
        await http.PostAsJsonAsync($"{Base}/orgs/{OrgId}/principals",
            new { name = "bob", role = "reader" });
    }

    public Task DisposeAsync()
    {
        _sirrd?.Kill(entireProcessTree: true);
        if (_dataDir != null)
        {
            try { Directory.Delete(_dataDir, recursive: true); } catch { }
        }
        return Task.CompletedTask;
    }

    private void ParseBootstrapInfo(string log)
    {
        var orgMatch = Regex.Match(log, @"org_id:\s+([0-9a-f]{32})");
        var keyMatch = Regex.Match(log, @"key=(sirr_key_[0-9a-f]+)");
        if (!orgMatch.Success || !keyMatch.Success)
            throw new InvalidOperationException($"Failed to parse bootstrap info from log:\n{log}");
        OrgId = orgMatch.Groups[1].Value;
        BootstrapKey = keyMatch.Groups[1].Value;
    }

    private async Task<string> WaitForBootstrapKeyAsync(int retries = 40)
    {
        for (int i = 0; i < retries; i++)
        {
            var log = string.Join("\n", _logLines);
            if (log.Contains("key=sirr_key_"))
                return log;
            await Task.Delay(100);
        }
        var finalLog = string.Join("\n", _logLines);
        throw new InvalidOperationException($"Bootstrap key not found after {retries * 100}ms. Log:\n{finalLog}");
    }

    private static void KillPortStalkers(int port)
    {
        try
        {
            // lsof -ti :PORT lists PIDs using the port; kill -9 them
            var psi = new ProcessStartInfo("bash", $"-c \"lsof -ti :{port} | xargs kill -9 2>/dev/null; true\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var p = Process.Start(psi);
            p?.WaitForExit(3000);
        }
        catch { }
    }

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

[CollectionDefinition("Integration")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix")]
public class IntegrationFixtureCollection : ICollectionFixture<SirrdFixture> { }
