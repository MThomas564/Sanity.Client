namespace Sanity.Client;

/// <summary>
/// The envelope returned by Sanity's GROQ query endpoint
/// (<c>/vX/data/query/{dataset}</c>).
/// </summary>
/// <typeparam name="T">The shape of the projected query result.</typeparam>
public sealed class SanityQueryResult<T>
{
    /// <summary>The GROQ query string that was executed, as echoed back by Sanity.</summary>
    public string Query { get; set; } = string.Empty;

    /// <summary>The deserialized query result.</summary>
    public T? Result { get; set; }

    /// <summary>Server-reported execution time in milliseconds, when available.</summary>
    public double? Ms { get; set; }

    /// <summary>
    /// The resultSourceMap payload, when requested; left untyped since its
    /// shape is only needed for advanced tooling (e.g. visual editing).
    /// </summary>
    public object? ResultSourceMap { get; set; }

    /// <summary>Returns true when Sanity reported a syntax error for the query.</summary>
    public string? SyntaxError { get; set; }
}
