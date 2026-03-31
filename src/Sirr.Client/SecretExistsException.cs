namespace Sirr;

/// <summary>
/// Thrown when attempting to set an org-scoped secret that already exists (HTTP 409 Conflict).
/// </summary>
public sealed class SecretExistsException : SirrException
{
    /// <summary>
    /// The key that already exists.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Creates a new <see cref="SecretExistsException"/> for the given key.
    /// </summary>
    public SecretExistsException(string key, string message)
        : base(409, message)
    {
        Key = key;
    }
}
