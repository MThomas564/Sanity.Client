using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Sanity.Client;

/// <summary>
/// Default <see cref="ISanityClient"/> implementation backed by
/// <see cref="HttpClient"/> and Sanity's HTTP GROQ query API.
/// </summary>
public sealed class SanityClient : ISanityClient
{
    // Mirrors @sanity/client's own GET/POST switchover threshold
    // (`getQuerySizeLimit` in its dataMethods.ts): once the encoded
    // query string (query + $params) would reach this many characters,
    // switch to POST with a JSON body instead, to stay well clear of
    // URL length limits imposed by proxies, load balancers, and browsers.
    private const int MaxGetQueryStringLength = 11264;

    private readonly HttpClient _httpClient;
    private readonly SanityClientOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    // Disambiguates for ActivatorUtilities (used internally by
    // AddHttpClient<ISanityClient, SanityClient>()), which otherwise can't
    // choose between this and the SanityClientOptions overload below since
    // both HttpClient and IOptions<SanityClientOptions> are resolvable
    // from the container.
    [ActivatorUtilitiesConstructor]
    public SanityClient(HttpClient httpClient, Microsoft.Extensions.Options.IOptions<SanityClientOptions> options)
        : this(httpClient, options.Value)
    {
    }

    public SanityClient(HttpClient httpClient, SanityClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        options.Validate();
        _options = options;
        _httpClient = httpClient;
        _jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        if (_httpClient.BaseAddress is null)
        {
            _httpClient.BaseAddress = options.BuildBaseUri();
        }

        if (!string.IsNullOrEmpty(options.Token) && _httpClient.DefaultRequestHeaders.Authorization is null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.Token);
        }

        if (options.Timeout is { } timeout)
        {
            // Throws InvalidOperationException if a request has already
            // gone out on this HttpClient instance; acceptable here since
            // this only ever runs once, in the constructor, before any
            // request can possibly have been sent through this instance.
            _httpClient.Timeout = timeout;
        }
    }

    /// <inheritdoc />
    public async Task<SanityQueryResult<T>> FetchAsync<T>(
        string query,
        object? @params = null,
        SanityFetchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("A GROQ query string is required.", nameof(query));
        }

        var paramsElement = SerializeParams(@params);
        var (queryString, queryStringLength) = BuildGetQueryString(query, paramsElement);
        var useGet = queryStringLength < MaxGetQueryStringLength;

        var relativeUrl = BuildRequestUrl(options, includeGetQueryString: useGet ? queryString : null);

        using var response = await SendAsync(relativeUrl, useGet, query, paramsElement, cancellationToken)
            .ConfigureAwait(false);

        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new SanityException(
                $"Sanity query failed with HTTP status {(int)response.StatusCode} ({response.ReasonPhrase}).",
                (int)response.StatusCode,
                body);
        }

        SanityQueryResult<T>? result;
        try
        {
            result = JsonSerializer.Deserialize<SanityQueryResult<T>>(body, _jsonOptions);
        }
        catch (JsonException ex)
        {
            throw new SanityException(
                "Failed to deserialize the Sanity query response.",
                (int)response.StatusCode,
                body,
                ex);
        }

        if (result is null)
        {
            throw new SanityException("Sanity returned an empty response body.", (int)response.StatusCode, body);
        }

        if (!string.IsNullOrEmpty(result.SyntaxError))
        {
            throw new SanityException($"GROQ syntax error: {result.SyntaxError}", (int)response.StatusCode, body);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<T?> FetchResultAsync<T>(
        string query,
        object? @params = null,
        SanityFetchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var result = await FetchAsync<T>(query, @params, options, cancellationToken).ConfigureAwait(false);
        return result.Result;
    }

    private async Task<HttpResponseMessage> SendAsync(
        string relativeUrl,
        bool useGet,
        string query,
        JsonElement? paramsElement,
        CancellationToken cancellationToken)
    {
        try
        {
            if (useGet)
            {
                return await _httpClient.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
            }

            // Query (and/or its params) is large enough that encoding it
            // into the URL risks hitting length limits somewhere in the
            // request path (browsers, proxies, load balancers) -- send it
            // as a JSON POST body instead, matching @sanity/client's own
            // behavior above the same threshold.
            var postBody = new SanityPostQueryBody(query, paramsElement);
            return await _httpClient.PostAsJsonAsync(relativeUrl, postBody, _jsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Genuine caller-initiated cancellation: let it propagate as
            // the exception type callers already expect, rather than
            // wrapping it.
            throw;
        }
        catch (TaskCanceledException ex)
        {
            // Not caller-initiated: this is HttpClient.Timeout (or a
            // POST's internal buffering) expiring.
            throw new SanityException(
                $"The request to the Sanity API timed out after {_httpClient.Timeout}.",
                innerException: ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SanityException("Failed to reach the Sanity API.", innerException: ex);
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or TaskCanceledException)
        {
            throw new SanityException("Failed to read the Sanity API response body.", innerException: ex);
        }
    }

    private JsonElement? SerializeParams(object? @params)
    {
        if (@params is null)
        {
            return null;
        }

        var element = JsonSerializer.SerializeToElement(@params, _jsonOptions);
        return element.ValueKind == JsonValueKind.Object ? element : null;
    }

    /// <summary>
    /// Builds the <c>query=...&amp;$param=...</c> portion that would be
    /// used for a GET request, and its length, so callers can decide
    /// whether to actually send it as GET or fall back to POST.
    /// </summary>
    private static (string QueryString, int Length) BuildGetQueryString(string query, JsonElement? paramsElement)
    {
        var sb = new StringBuilder();
        sb.Append("query=").Append(Uri.EscapeDataString(query));

        if (paramsElement is { ValueKind: JsonValueKind.Object } element)
        {
            foreach (var property in element.EnumerateObject())
            {
                sb.Append("&$")
                  .Append(Uri.EscapeDataString(property.Name))
                  .Append('=')
                  .Append(Uri.EscapeDataString(property.Value.GetRawText()));
            }
        }

        var queryString = sb.ToString();
        return (queryString, queryString.Length);
    }

    private string BuildRequestUrl(SanityFetchOptions? options, string? includeGetQueryString)
    {
        var sb = new StringBuilder();
        sb.Append(_options.ApiVersion)
          .Append("/data/query/")
          .Append(Uri.EscapeDataString(_options.Dataset));

        var hasQuery = false;

        if (includeGetQueryString is not null)
        {
            sb.Append('?').Append(includeGetQueryString);
            hasQuery = true;
        }

        if (options?.Perspective is { } perspective)
        {
            sb.Append(hasQuery ? '&' : '?').Append("perspective=").Append(ToPerspectiveString(perspective));
            hasQuery = true;
        }

        if (options?.ResultSourceMap is { } sourceMapMode and not SanitySourceMapMode.None)
        {
            sb.Append(hasQuery ? '&' : '?').Append("resultSourceMap=").Append(ToSourceMapString(sourceMapMode));
        }

        return sb.ToString();
    }

    private static string ToPerspectiveString(SanityPerspective perspective) => perspective switch
    {
        SanityPerspective.Published => "published",
        SanityPerspective.Raw => "raw",
        SanityPerspective.Drafts => "drafts",
        _ => throw new ArgumentOutOfRangeException(nameof(perspective), perspective, null),
    };

    private static string ToSourceMapString(SanitySourceMapMode mode) => mode switch
    {
        SanitySourceMapMode.Enabled => "true",
        SanitySourceMapMode.WithKeyArraySelector => "withKeyArraySelector",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    /// <summary>JSON body shape for the POST fallback (large query/params).</summary>
    private sealed record SanityPostQueryBody(string Query, JsonElement? Params);
}
