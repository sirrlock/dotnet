namespace Sirr;

/// <summary>
/// Sets environment variables from a dictionary and restores originals on dispose.
/// </summary>
public sealed class EnvScope : IAsyncDisposable, IDisposable
{
    private readonly Dictionary<string, string?> _originals;
    private int _disposed;

    internal EnvScope(IReadOnlyDictionary<string, string> secrets)
    {
        _originals = new Dictionary<string, string?>(secrets.Count);

        foreach (var (key, value) in secrets)
        {
            _originals[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Restores environment variables to their original values.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return;
        }

        foreach (var (key, original) in _originals)
        {
            Environment.SetEnvironmentVariable(key, original);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
