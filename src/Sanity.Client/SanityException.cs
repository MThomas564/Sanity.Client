namespace Sanity.Client;

/// <summary>
/// Thrown when a request to the Sanity API fails, either at the transport
/// level or because Sanity returned an error payload (e.g. a GROQ syntax
/// error or an HTTP error status).
/// </summary>
public sealed class SanityException : Exception
{
    /// <summary>The HTTP status code returned by Sanity, when available.</summary>
    public int? StatusCode { get; }

    /// <summary>The raw response body returned by Sanity, when available.</summary>
    public string? ResponseBody { get; }

    public SanityException(string message, int? statusCode = null, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}
