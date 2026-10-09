using System.Text.Json;

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
    /// The Content Source Map, present when the request was made with
    /// <see cref="SanityFetchOptions.ResultSourceMap"/> set to anything
    /// other than <see cref="SanitySourceMapMode.None"/>. Left as a raw
    /// <see cref="JsonElement"/> since its structure is only needed by
    /// advanced tooling (e.g. <c>Sanity.Client.Presentation</c>'s stega
    /// encoding / click-to-edit support), which passes it through to the
    /// real Sanity JS verbatim rather than re-parsing it in .NET.
    /// </summary>
    public JsonElement? ResultSourceMap { get; set; }

    /// <summary>Returns true when Sanity reported a syntax error for the query.</summary>
    public string? SyntaxError { get; set; }
}
