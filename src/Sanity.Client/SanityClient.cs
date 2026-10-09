using System.Net.Http.Headers;
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

        var relativeUrl = BuildQueryUrl(query, @params, options);

        HttpResponseMessage response;
        string body;
        try
        {
            response = await _httpClient.GetAsync(relativeUrl, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new SanityException("Failed to reach the Sanity API.", innerException: ex);
        }

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

    private string BuildQueryUrl(string query, object? @params, SanityFetchOptions? options)
    {
        var sb = new StringBuilder();
        sb.Append(_options.ApiVersion)
          .Append("/data/query/")
          .Append(Uri.EscapeDataString(_options.Dataset))
          .Append("?query=")
          .Append(Uri.EscapeDataString(query));

        if (options?.Perspective is { } perspective)
        {
            sb.Append("&perspective=").Append(ToPerspectiveString(perspective));
        }

        if (options?.ResultSourceMap is { } sourceMapMode and not SanitySourceMapMode.None)
        {
            sb.Append("&resultSourceMap=").Append(ToSourceMapString(sourceMapMode));
        }

        if (@params is not null)
        {
            var element = JsonSerializer.SerializeToElement(@params, _jsonOptions);
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    sb.Append("&$")
                      .Append(Uri.EscapeDataString(property.Name))
                      .Append('=')
                      .Append(Uri.EscapeDataString(property.Value.GetRawText()));
                }
            }
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
}
