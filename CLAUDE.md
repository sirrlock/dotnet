# Sirr.Client (.NET) — Claude Development Guide

## Purpose

.NET HTTP client for the Sirr API. Published to NuGet as `Sirr.Client`.
Targets .NET 8+. Async-first, built on `HttpClient`.

## Planned API Surface

```csharp
public class SirrClient : IDisposable
{
    SirrClient(SirrOptions options)
    SirrClient(string server, string token)

    Task PushAsync(string key, string value, TimeSpan? ttl = null, int? reads = null, CancellationToken ct = default)
    Task<string?> GetAsync(string key, CancellationToken ct = default)          // null if burned/expired
    Task DeleteAsync(string key, CancellationToken ct = default)
    Task<IReadOnlyList<SecretMeta>> ListAsync(CancellationToken ct = default)
    Task<IReadOnlyDictionary<string, string>> PullAllAsync(CancellationToken ct = default)
    Task<int> PruneAsync(CancellationToken ct = default)
}
```

## Stack

- .NET 8+, C# 12
- `System.Net.Http.HttpClient` — no third-party HTTP libs
- `System.Text.Json` for serialization
- `xunit` for tests
- NuGet packaging via `<PackageId>Sirr.Client</PackageId>` in .csproj

## Key Rules

- `GetAsync` returns `null` on 404 — do not throw
- All other non-2xx responses throw `SirrException`
- Never log secret values
- Accept `CancellationToken` on all async methods
- Register `SirrClient` as a singleton via `IHttpClientFactory` in DI scenarios

## Pre-Commit Checklist

Before every commit and push, review and update if needed:

1. **README.md** — Does it reflect new methods or behavior?
2. **CLAUDE.md** — New constraints or API decisions worth recording?
