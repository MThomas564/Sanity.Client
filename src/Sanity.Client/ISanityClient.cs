namespace Sanity.Client;

/// <summary>
/// A read-only client for querying a Sanity.io dataset using GROQ.
/// </summary>
public interface ISanityClient
{
    /// <summary>
    /// Executes a GROQ query and returns the full response envelope
    /// (query, result, timing metadata).
    /// </summary>
    /// <typeparam name="T">The shape of the projected query result.</typeparam>
    /// <param name="query">The GROQ query string.</param>
    /// <param name="params">
    /// Optional query parameters referenced in the GROQ query as
    /// <c>$paramName</c>. Any object will be serialized to JSON and each
    /// top-level property sent as a <c>$propertyName</c> query parameter.
    /// </param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<SanityQueryResult<T>> FetchAsync<T>(
        string query,
        object? @params = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a GROQ query and returns just the deserialized result,
    /// unwrapped from the response envelope. Convenience wrapper over
    /// <see cref="FetchAsync{T}(string, object?, CancellationToken)"/>.
    /// </summary>
    /// <typeparam name="T">The shape of the projected query result.</typeparam>
    /// <param name="query">The GROQ query string.</param>
    /// <param name="params">Optional GROQ query parameters.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task<T?> FetchResultAsync<T>(
        string query,
        object? @params = null,
        CancellationToken cancellationToken = default);
}
