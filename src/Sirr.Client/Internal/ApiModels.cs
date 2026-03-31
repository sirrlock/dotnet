using System.Text.Json.Serialization;

namespace Sirr.Internal;

/// <summary>Public dead-drop push: POST /secrets (no key field).</summary>
internal sealed class PublicPushRequest
{
    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("ttl_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TtlSeconds { get; init; }

    [JsonPropertyName("max_reads")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxReads { get; init; }
}

/// <summary>Public dead-drop push response: {"id": "hex64"}.</summary>
internal sealed class PublicPushResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>Org-scoped set: POST /orgs/{org}/secrets.</summary>
internal sealed class OrgSetRequest
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }

    [JsonPropertyName("ttl_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TtlSeconds { get; init; }

    [JsonPropertyName("max_reads")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxReads { get; init; }

    [JsonPropertyName("delete")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Delete { get; init; }

    [JsonPropertyName("webhook_url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? WebhookUrl { get; init; }

    [JsonPropertyName("allowed_keys")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? AllowedKeys { get; init; }
}

/// <summary>Org-scoped set response: {"key": "..."}.</summary>
internal sealed class OrgSetResponse
{
    [JsonPropertyName("key")]
    public required string Key { get; init; }
}

internal sealed class GetSecretResponse
{
    // Public endpoint returns {"id": "...", "value": "..."}
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    // Org endpoint returns {"key": "...", "value": "..."}
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    [JsonPropertyName("value")]
    public required string Value { get; init; }
}

/// <summary>Error response body with optional machine-readable error code.</summary>
internal sealed class ApiErrorResponse
{
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

internal sealed class ListSecretsResponse
{
    [JsonPropertyName("secrets")]
    public required SecretMeta[] Secrets { get; init; }
}

internal sealed class DeleteSecretResponse
{
    [JsonPropertyName("deleted")]
    public required bool Deleted { get; init; }
}

internal sealed class PruneResponse
{
    [JsonPropertyName("pruned")]
    public required int Pruned { get; init; }
}

internal sealed class HealthResponse
{
    [JsonPropertyName("status")]
    public required string Status { get; init; }
}

internal sealed class AuditEventsResponse
{
    [JsonPropertyName("events")]
    public required AuditEvent[] Events { get; init; }
}

internal sealed class CreateWebhookRequest
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("events")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Events { get; init; }
}

internal sealed class ListWebhooksResponse
{
    [JsonPropertyName("webhooks")]
    public required Webhook[] Webhooks { get; init; }
}

internal sealed class DeletedResponse
{
    [JsonPropertyName("deleted")]
    public required bool Deleted { get; init; }
}

internal sealed class UpdateMeRequest
{
    [JsonPropertyName("metadata")]
    public required Dictionary<string, string> Metadata { get; init; }
}

internal sealed class CreateMeKeyRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("valid_for_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ValidForSeconds { get; init; }

    [JsonPropertyName("valid_before")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ValidBefore { get; init; }
}

internal sealed class CreateOrgRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Metadata { get; init; }
}

internal sealed class ListOrgsResponse
{
    [JsonPropertyName("orgs")]
    public required OrgResponse[] Orgs { get; init; }
}

internal sealed class CreatePrincipalRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Metadata { get; init; }
}

internal sealed class ListPrincipalsResponse
{
    [JsonPropertyName("principals")]
    public required PrincipalResponse[] Principals { get; init; }
}

internal sealed class CreateRoleRequest
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>Permission letter string e.g. "CWRD".</summary>
    [JsonPropertyName("permissions")]
    public required string Permissions { get; init; }
}

internal sealed class ListRolesResponse
{
    [JsonPropertyName("roles")]
    public required RoleResponse[] Roles { get; init; }
}

internal sealed class PatchSecretRequest
{
    [JsonPropertyName("value")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Value { get; init; }

    [JsonPropertyName("ttl_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? TtlSeconds { get; init; }

    [JsonPropertyName("max_reads")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxReads { get; init; }
}

internal sealed class PatchSecretResponse
{
    [JsonPropertyName("key")]
    public string? Key { get; init; }
}
