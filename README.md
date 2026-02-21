# Sirr.Client (.NET)

[![NuGet version](https://img.shields.io/nuget/v/Sirr.Client)](https://www.nuget.org/packages/Sirr.Client/)
[![NuGet downloads](https://img.shields.io/nuget/dt/Sirr.Client)](https://www.nuget.org/packages/Sirr.Client/)
[![CI](https://github.com/sirrlock/dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/sirrlock/dotnet/actions/workflows/ci.yml)
[![.NET](https://img.shields.io/badge/.NET-%3E%3D8-purple)](https://dotnet.microsoft.com)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![GitHub stars](https://img.shields.io/github/stars/sirrlock/dotnet)](https://github.com/sirrlock/dotnet)
[![Last commit](https://img.shields.io/github/last-commit/sirrlock/dotnet)](https://github.com/sirrlock/dotnet)

**Ephemeral secrets for .NET AI applications. Credentials that expire by design.**

`Sirr.Client` is the .NET client for [Sirr](https://github.com/sirrlock/sirr) — a self-hosted vault where every secret expires by read count, by time, or both. Built for .NET applications using Semantic Kernel, Microsoft.Extensions.AI, Azure OpenAI SDK, or any AI orchestration layer that needs scoped, disposable credentials.

---

## The Problem It Solves

.NET enterprise applications increasingly embed AI agents for document processing, customer service, data analysis, and internal tooling. These agents need real credentials: connection strings, API keys, service tokens. The standard .NET approach — `IConfiguration`, `Azure Key Vault`, `AWS Secrets Manager` — stores those credentials permanently and requires manual rotation.

Sirr provides a different abstraction: **credentials that self-destruct after the agent is done.** You define the maximum lifetime and read budget. The server enforces it. You don't need to schedule cleanup jobs or remember to revoke.

```csharp
// Give the agent exactly 1 read of the connection string — 30-minute window
await sirr.PushAsync("COSMOS_URL", connectionString, reads: 1, ttl: TimeSpan.FromMinutes(30));

// Agent reads it → read counter hits 1 → record deleted
var url = await sirr.GetAsync("COSMOS_URL");  // returns value
url = await sirr.GetAsync("COSMOS_URL");      // returns null — burned
```

---

## Install

```bash
dotnet add package Sirr.Client
```

Targets .NET 8+. No third-party HTTP dependencies — built on `System.Net.Http` and `System.Text.Json`.

---

## Usage

```csharp
using Sirr;

var sirr = new SirrClient(new SirrOptions
{
    Server = Environment.GetEnvironmentVariable("SIRR_SERVER") ?? "http://localhost:39999",
    Token  = Environment.GetEnvironmentVariable("SIRR_TOKEN")!,
});

// Push a one-time secret
await sirr.PushAsync("API_KEY", "sk-...", ttl: TimeSpan.FromHours(1), reads: 1);

// Push with a webhook notification on read/burn
await sirr.PushAsync("API_KEY", "sk-...", reads: 1, webhookUrl: "https://hooks.example.com/sirr");

// Push in seal mode — secret blocks after reads exhausted, refreshable via PatchAsync
await sirr.PushAsync("RENEWABLE", "value", reads: 5, sealOnExpiry: true);

// Push with access restricted to specific API key IDs (org-scoped only)
await sirr.PushAsync("TEAM_SECRET", "value", allowedKeys: ["key_abc123", "key_def456"]);

// Check existence without consuming a read
SecretStatus? status = await sirr.HeadAsync("API_KEY");
// status?.ReadCount, status?.ReadsRemaining, status?.IsSealed, status?.ExpiresAt

// Retrieve — null if burned or expired
string? value = await sirr.GetAsync("API_KEY");

// Pull all into a dictionary
IDictionary<string, string> secrets = await sirr.PullAllAsync();

// Update value, TTL, or read budget on a seal-mode secret (resets read counter)
await sirr.PatchAsync("RENEWABLE", value: "new-value", ttl: TimeSpan.FromHours(2), reads: 5);

// Delete immediately
await sirr.DeleteAsync("API_KEY");

// List active secrets (metadata only — values never returned by list)
IReadOnlyList<SecretMeta> list = await sirr.ListAsync();

// Prune expired secrets
int pruned = await sirr.PruneAsync();
```

### Dependency Injection

```csharp
// Program.cs
builder.Services.AddSirrClient(options =>
{
    options.Server = builder.Configuration["Sirr:Server"]!;
    options.Token  = builder.Configuration["Sirr:Token"]!;
});

// Inject ISirrClient anywhere
public class AgentService(ISirrClient sirr)
{
    public async Task RunAsync()
    {
        var key = await sirr.GetAsync("AGENT_KEY");
        // ...
    }
}
```

---

## AI Workflows

### Semantic Kernel plugin with scoped credential

```csharp
using Microsoft.SemanticKernel;

public class DatabasePlugin(ISirrClient sirr)
{
    [KernelFunction, Description("Query the production database")]
    public async Task<string> QueryAsync(string sql)
    {
        var connStr = await sirr.GetAsync("AGENT_DB");
        if (connStr is null) throw new InvalidOperationException("DB credential expired or already used");

        await using var conn = new NpgsqlConnection(connStr);
        // run query...
    }
}
```

### Azure OpenAI agent with burn-after-use API key

```csharp
// Push a time-limited key before starting the agent session
await sirr.PushAsync("AOAI_KEY", openAiKey, reads: 1, ttl: TimeSpan.FromMinutes(60));

var client = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(await sirr.GetAsync("AOAI_KEY") ?? throw new Exception("Key expired"))
);

// Key was consumed on GetAsync — it no longer exists in Sirr
```

### Microsoft.Extensions.AI with temporary credentials

```csharp
// Inject secrets into an AI pipeline scope
await using var scope = await sirr.CreateEnvScopeAsync(); // sets env vars
// All Sirr secrets available as environment variables within this scope
await RunAgentPipelineAsync();
// Scope disposed — env vars removed
```

### Hosted Service with read-budget enforcement

```csharp
public class DataIngestionService(ISirrClient sirr) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Push a credential valid for 5 reads — retry-safe
        await sirr.PushAsync("SOURCE_API_KEY", apiKey, reads: 5, ttl: TimeSpan.FromHours(2));

        for (int i = 0; i < 5; i++)
        {
            var key = await sirr.GetAsync("SOURCE_API_KEY", ct);
            if (key is null) break; // budget exhausted
            await IngestBatch(key, ct);
        }
    }
}
```

---

## Multi-Tenant / Org Mode

Set `Org` on `SirrOptions` to route all secret, audit, webhook, and prune operations through an org-scoped path (`/orgs/{org}/secrets/...`).

```csharp
var sirr = new SirrClient(new SirrOptions
{
    Server = "http://localhost:39999",
    Token  = Environment.GetEnvironmentVariable("SIRR_TOKEN")!,
    Org    = "acme",
});

// All calls now hit /orgs/acme/secrets/*, /orgs/acme/audit, etc.
await sirr.PushAsync("DB_URL", "postgres://...");
var secrets = await sirr.ListAsync();
```

### Dependency Injection with Org

```csharp
builder.Services.AddSirrClient(options =>
{
    options.Server = builder.Configuration["Sirr:Server"]!;
    options.Token  = builder.Configuration["Sirr:Token"]!;
    options.Org    = builder.Configuration["Sirr:Org"]; // optional
});
```

### /me Endpoints

```csharp
// Get the authenticated principal's profile
MeResponse me = await sirr.GetMeAsync();
// me.Id, me.Name, me.Role, me.OrgId, me.Metadata, me.CreatedAt, me.Keys

// Update metadata
MeResponse updated = await sirr.UpdateMeAsync(new Dictionary<string, string>
{
    ["team"] = "platform",
    ["env"]  = "prod",
});

// Create a personal API key (shown only once)
KeyCreateResult key = await sirr.CreateMeKeyAsync("ci-key");
Console.WriteLine(key.Key); // store this — not retrievable again

// Create a time-limited key — either by duration or absolute expiry (Unix epoch seconds)
KeyCreateResult limited = await sirr.CreateMeKeyAsync("deploy-key", validForSeconds: 3600);
KeyCreateResult expires = await sirr.CreateMeKeyAsync("until-key", validBefore: 1800000000L);

// Delete a personal key by ID
await sirr.DeleteMeKeyAsync(key.Id);
```

### Admin Endpoints

```csharp
// --- Orgs ---
OrgResponse org = await sirr.CreateOrgAsync("Acme Corp", metadata: new() { ["tier"] = "pro" });
IReadOnlyList<OrgResponse> orgs = await sirr.ListOrgsAsync();
await sirr.DeleteOrgAsync(org.Id);

// --- Principals ---
PrincipalResponse principal = await sirr.CreatePrincipalAsync(
    orgId: org.Id,
    role: "member",
    name: "Alice",
    metadata: new() { ["email"] = "alice@acme.com" });

IReadOnlyList<PrincipalResponse> principals = await sirr.ListPrincipalsAsync(org.Id);
await sirr.DeletePrincipalAsync(org.Id, principal.Id);

// --- Roles ---
// permissions is a letter string: C=Create W=Write R=Read D=Delete S=Seal
RoleResponse role = await sirr.CreateRoleAsync(org.Id, "viewer", permissions: "R");
IReadOnlyList<RoleResponse> roles = await sirr.ListRolesAsync(org.Id);
await sirr.DeleteRoleAsync(org.Id, role.Name);
```

---

## Related

| Package | Description |
|---------|-------------|
| [sirr](https://github.com/sirrlock/sirr) | Rust monorepo: `sirrd` server + `sirr` CLI |
| [@sirrlock/mcp](https://github.com/sirrlock/mcp) | MCP server for AI assistants |
| [@sirrlock/node](https://github.com/sirrlock/node) | Node.js / TypeScript SDK |
| [sirr (PyPI)](https://github.com/sirrlock/python) | Python SDK |
| [sirr.dev](https://sirr.dev) | Documentation |
| [sirrlock.com](https://sirrlock.com) | Managed cloud + license keys |
