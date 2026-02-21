# Sirr.Client (.NET)

.NET client for [Sirr](https://github.com/SirrVault/sirr) — ephemeral secret management.

> Work in progress.

## Install

```bash
dotnet add package Sirr.Client
```

## Usage

```csharp
using Sirr;

var sirr = new SirrClient(new SirrOptions
{
    Server = Environment.GetEnvironmentVariable("SIRR_SERVER") ?? "http://localhost:8080",
    Token  = Environment.GetEnvironmentVariable("SIRR_TOKEN")!,
});

// Push a one-time secret
await sirr.PushAsync("API_KEY", "sk-...", ttl: TimeSpan.FromHours(1), reads: 1);

// Retrieve (null if burned or expired)
string? value = await sirr.GetAsync("API_KEY");

// Pull all secrets into a dictionary
IDictionary<string, string> secrets = await sirr.PullAllAsync();

// Delete immediately
await sirr.DeleteAsync("API_KEY");
```

## Related

- [SirrVault/sirr](https://github.com/SirrVault/sirr) — server
- [SirrVault/cli](https://github.com/SirrVault/cli) — CLI
- [SirrVault/node](https://github.com/SirrVault/node) — Node.js client
- [SirrVault/python](https://github.com/SirrVault/python) — Python client
