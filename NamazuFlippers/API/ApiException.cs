namespace NamazuFlippers.API;

/// <summary>
/// Exception thrown when the Saddlebag Exchange API returns an error
/// or when the HTTP request fails after all retries are exhausted.
/// </summary>
public sealed class ApiException : Exception
{
    /// <summary>HTTP status code, if the server returned one. Null for network errors.</summary>
    public int? StatusCode { get; }

    /// <summary>True if the error is transient and a retry might succeed (5xx, network timeout).</summary>
    public bool IsRetryable { get; }

    public ApiException(string message, int? statusCode = null, bool isRetryable = false)
        : base(message)
    {
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }

    public ApiException(string message, Exception innerException, int? statusCode = null, bool isRetryable = false)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsRetryable = isRetryable;
    }
}
