using System.Net;
using System.Text.Json.Serialization;

namespace Sanity.Client.Tests;

public sealed class SanityClientTests
{
    private static (SanityClient Client, FakeHttpMessageHandler Handler) CreateClient(
        HttpStatusCode statusCode,
        string responseBody,
        Action<SanityClientOptions>? configure = null)
    {
        var options = new SanityClientOptions
        {
            ProjectId = "test123",
            Dataset = "production",
            ApiVersion = "v2024-01-01",
        };
        configure?.Invoke(options);

        var handler = new FakeHttpMessageHandler(statusCode, responseBody);
        var httpClient = new HttpClient(handler);
        var client = new SanityClient(httpClient, options);
        return (client, handler);
    }

    [Fact]
    public async Task FetchAsync_ReturnsDeserializedResult()
    {
        const string body = """
            { "query": "*[_type == \"post\"]", "result": [{"_id":"abc","title":"Hello"}], "ms": 3 }
            """;
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        var result = await client.FetchAsync<List<Post>>("*[_type == \"post\"]");

        Assert.NotNull(result.Result);
        Assert.Single(result.Result!);
        Assert.Equal("abc", result.Result![0].Id);
        Assert.Equal("Hello", result.Result![0].Title);
        Assert.Equal(3, result.Ms);
        Assert.Contains("data/query/production", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task FetchResultAsync_UnwrapsResult()
    {
        const string body = """{ "query": "*", "result": {"_id":"x","title":"T"} }""";
        var (client, _) = CreateClient(HttpStatusCode.OK, body);

        var post = await client.FetchResultAsync<Post>("*[_type == \"post\"][0]");

        Assert.NotNull(post);
        Assert.Equal("x", post!.Id);
    }

    [Fact]
    public async Task FetchAsync_SendsParamsAsJsonEncodedQueryString()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        await client.FetchAsync<object>(
            "*[_type == \"post\" && slug.current == $slug][0]",
            new { slug = "hello-world" });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("$slug=\"hello-world\"", uri);
    }

    [Fact]
    public async Task FetchAsync_SendsPerspectiveAndResultSourceMapWhenRequested()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        await client.FetchAsync<object>(
            "*[_type == \"post\"]",
            options: new SanityFetchOptions
            {
                Perspective = SanityPerspective.Drafts,
                ResultSourceMap = SanitySourceMapMode.WithKeyArraySelector,
            });

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("perspective=drafts", uri);
        Assert.Contains("resultSourceMap=withKeyArraySelector", uri);
    }

    [Fact]
    public async Task FetchAsync_OmitsPerspectiveAndSourceMapByDefault()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        await client.FetchAsync<object>("*[_type == \"post\"]");

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.DoesNotContain("perspective=", uri);
        Assert.DoesNotContain("resultSourceMap=", uri);
    }

    [Fact]
    public async Task FetchAsync_DeserializesResultSourceMap()
    {
        const string body = """
            {
              "query": "*",
              "result": {"title": "Hello"},
              "resultSourceMap": {
                "documents": [{"_id": "post-1", "_type": "post"}],
                "paths": ["$['title']"],
                "mappings": {}
              }
            }
            """;
        var (client, _) = CreateClient(HttpStatusCode.OK, body);

        var result = await client.FetchAsync<object>(
            "*[_type == \"post\"][0]",
            options: new SanityFetchOptions { ResultSourceMap = SanitySourceMapMode.Enabled });

        Assert.NotNull(result.ResultSourceMap);
        Assert.True(result.ResultSourceMap!.Value.TryGetProperty("documents", out _));
    }

    [Fact]
    public async Task FetchAsync_ThrowsSanityException_OnHttpError()
    {
        var (client, _) = CreateClient(HttpStatusCode.Unauthorized, "unauthorized");

        var ex = await Assert.ThrowsAsync<SanityException>(
            () => client.FetchAsync<object>("*[_type == \"post\"]"));

        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task FetchAsync_ThrowsSanityException_OnGroqSyntaxError()
    {
        const string body = """{ "query": "bad(", "error": {}, "syntaxError": "Unexpected end of input" }""";
        var (client, _) = CreateClient(HttpStatusCode.OK, body);

        var ex = await Assert.ThrowsAsync<SanityException>(
            () => client.FetchAsync<object>("bad("));

        Assert.Contains("syntax error", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Options_UsesCdnHostByDefault()
    {
        var options = new SanityClientOptions { ProjectId = "test123", Dataset = "production" };
        var uri = options.BuildBaseUri();
        Assert.Equal("test123.apicdn.sanity.io", uri.Host);
    }

    [Fact]
    public void Options_UsesApiHost_WhenTokenSupplied()
    {
        var options = new SanityClientOptions
        {
            ProjectId = "test123",
            Dataset = "production",
            Token = "secret",
        };
        var uri = options.BuildBaseUri();
        Assert.Equal("test123.api.sanity.io", uri.Host);
    }

    [Fact]
    public void Options_Validate_ThrowsWhenProjectIdMissing()
    {
        var options = new SanityClientOptions { ProjectId = "", Dataset = "production" };
        Assert.Throws<InvalidOperationException>(() => new SanityClient(new HttpClient(), options));
    }

    [Fact]
    public async Task FetchResultAsync_PopulatesSanityDocumentBaseFields()
    {
        const string body = """
            {
              "query": "*",
              "result": [
                {
                  "_id": "movie_1",
                  "_type": "movie",
                  "_rev": "rev-1",
                  "_createdAt": "2020-01-01T00:00:00Z",
                  "_updatedAt": "2020-02-02T00:00:00Z",
                  "title": "Example"
                }
              ]
            }
            """;
        var (client, _) = CreateClient(HttpStatusCode.OK, body);

        var movies = await client.FetchResultAsync<List<Movie>>("*[_type == \"movie\"]");

        Assert.NotNull(movies);
        var movie = Assert.Single(movies!);
        Assert.Equal("movie_1", movie.Id);
        Assert.Equal("movie", movie.Type);
        Assert.Equal("rev-1", movie.Rev);
        Assert.Equal(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero), movie.CreatedAt);
        Assert.Equal(new DateTimeOffset(2020, 2, 2, 0, 0, 0, TimeSpan.Zero), movie.UpdatedAt);
        Assert.Equal("Example", movie.Title);
        Assert.IsAssignableFrom<ISanityDocument>(movie);
    }

    [Fact]
    public async Task FetchAsync_UsesGet_ForShortQueries()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        await client.FetchAsync<object>("*[_type == \"post\"]");

        Assert.Equal(HttpMethod.Get, handler.LastRequest!.Method);
        Assert.Null(handler.LastRequestBody);
    }

    [Fact]
    public async Task FetchAsync_FallsBackToPost_ForVeryLongQueries()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        // Pad the query well past the 11264-character GET threshold.
        var longQuery = "*[_type == \"post\" && (" +
            string.Join(" || ", Enumerable.Range(0, 1000).Select(i => $"slug.current == \"post-{i}\"")) +
            ")]";
        Assert.True(longQuery.Length > 11264, "test query should exceed the GET threshold");

        await client.FetchAsync<object>(longQuery);

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.NotNull(handler.LastRequestBody);
        var sentQuery = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!)
            .RootElement.GetProperty("query").GetString();
        Assert.Equal(longQuery, sentQuery);
        // The long query must not have been encoded into the URL itself.
        Assert.DoesNotContain("query=", handler.LastRequest.RequestUri!.Query);
    }

    [Fact]
    public async Task FetchAsync_PostFallback_IncludesParamsInBody()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        var longQuery = "*[_type == \"post\" && slug.current == $slug && (" +
            string.Join(" || ", Enumerable.Range(0, 1000).Select(i => $"category == \"cat-{i}\"")) +
            ")]";

        await client.FetchAsync<object>(longQuery, new { slug = "hello-world" });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        var sentParams = System.Text.Json.JsonDocument.Parse(handler.LastRequestBody!)
            .RootElement.GetProperty("params");
        Assert.Equal("hello-world", sentParams.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task FetchAsync_PostFallback_StillAppliesPerspectiveAndSourceMapAsQueryParams()
    {
        const string body = """{ "query": "*", "result": null }""";
        var (client, handler) = CreateClient(HttpStatusCode.OK, body);

        var longQuery = "*[_type == \"post\" && (" +
            string.Join(" || ", Enumerable.Range(0, 1000).Select(i => $"slug.current == \"post-{i}\"")) +
            ")]";

        await client.FetchAsync<object>(
            longQuery,
            options: new SanityFetchOptions
            {
                Perspective = SanityPerspective.Drafts,
                ResultSourceMap = SanitySourceMapMode.WithKeyArraySelector,
            });

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        var uri = handler.LastRequest.RequestUri!.ToString();
        Assert.Contains("perspective=drafts", uri);
        Assert.Contains("resultSourceMap=withKeyArraySelector", uri);
    }

    [Fact]
    public async Task FetchAsync_ThrowsSanityException_OnTimeout()
    {
        var options = new SanityClientOptions
        {
            ProjectId = "test123",
            Dataset = "production",
            Timeout = TimeSpan.FromMilliseconds(20),
        };

        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "query": "*", "result": null }""")
        {
            Delay = TimeSpan.FromSeconds(5),
        };
        using var httpClient = new HttpClient(handler);
        var client = new SanityClient(httpClient, options);

        var ex = await Assert.ThrowsAsync<SanityException>(
            () => client.FetchAsync<object>("*[_type == \"post\"]"));

        Assert.Contains("timed out", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_PropagatesCallerCancellation_WithoutWrapping()
    {
        var (client, _) = CreateClient(HttpStatusCode.OK, """{ "query": "*", "result": null }""");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.FetchAsync<object>("*[_type == \"post\"]", cancellationToken: cts.Token));
    }

    private sealed class Post
    {
        [JsonPropertyName("_id")]
        public string? Id { get; set; }
        public string? Title { get; set; }
    }

    private sealed class Movie : SanityDocument
    {
        public string Title { get; set; } = string.Empty;
    }
}
