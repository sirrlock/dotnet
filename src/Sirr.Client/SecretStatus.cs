namespace Sirr;

/// <summary>
/// Metadata returned by <see cref="ISirrClient.HeadAsync"/> via response headers.
/// The secret's value is never read, so the read counter is not incremented.
/// Returns <c>null</c> from <see cref="ISirrClient.HeadAsync"/> when the secret does not exist or has expired.
/// </summary>
public sealed record SecretStatus
{
    /// <summary>Number of times this secret has been read so far.</summary>
    public int ReadCount { get; init; }

    /// <summary>
    /// Reads remaining before the secret burns or seals.
    /// <c>null</c> means unlimited reads.
    /// </summary>
    public int? ReadsRemaining { get; init; }

    /// <summary>
    /// <c>true</c> = burn-after-read (deleted when read budget exhausted).
    /// <c>false</c> = seal mode (blocked but patchable via <see cref="ISirrClient.PatchAsync"/>).
    /// </summary>
    public bool Delete { get; init; }

    /// <summary>Unix epoch seconds when the secret was created.</summary>
    public long CreatedAt { get; init; }

    /// <summary>Unix epoch seconds when the secret expires. <c>null</c> if no TTL was set.</summary>
    public long? ExpiresAt { get; init; }

    /// <summary>
    /// <c>true</c> when the read budget is exhausted on a seal-mode secret (410 Gone).
    /// The value is blocked until <see cref="ISirrClient.PatchAsync"/> resets the counter.
    /// </summary>
    public bool IsSealed { get; init; }
}
