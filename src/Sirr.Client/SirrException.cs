namespace Sirr;

/// <summary>
/// Thrown when the Sirr API returns a non-success status code.
/// </summary>
public class SirrException : Exception
{
    /// <summary>
    /// HTTP status code from the Sirr API response.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Creates a new <see cref="SirrException"/> with the given status code and message.
    /// </summary>
    public SirrException(int statusCode, string message)
        : base($"Sirr API error {statusCode}: {message}")
    {
        StatusCode = statusCode;
    }
}
