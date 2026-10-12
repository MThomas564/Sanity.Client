using System.Net;
using System.Text;
using Sanity.Client.Linq;

namespace Sanity.Client.Tests.Linq;

/// <summary>
/// Exercises actual execution (not just translation): what GROQ gets
/// sent for each terminal operator, and how the response is
/// materialized -- including client-side <c>Select</c> projection,
/// Count/Any semantics, and First/Single error behavior.
/// </summary>
public sealed class SanityQueryableExecutionTests
{
    private static (ISanityClient Client, RoutedFakeHandler Handler) CreateClient(Func<string, string> responder)
    {
        var handler = new RoutedFakeHandler(responder);
        var client = new SanityClient(
            new HttpClient(handler),
            new SanityClientOptions { ProjectId = "test123", Dataset = "production" });
        return (client, handler);
    }

    [Fact]
    public async Task ToListAsync_FetchesAndDeserializesTheFullDocumentList()
    {
        var (client, handler) = CreateClient(_ => """
            {"query":"*","result":[{"_id":"m1","_type":"movie","title":"Alien","releaseYear":1979}]}
            """);

        var movies = await client.Query<Movie>().Where(m => m.ReleaseYear > 1970).ToListAsync();

        Assert.Equal("""*[_type == "movie" && (releaseYear > 1970)]""", handler.LastQuery);
        var movie = Assert.Single(movies);
        Assert.Equal("Alien", movie.Title);
        Assert.Equal(1979, movie.ReleaseYear);
    }

    [Fact]
    public async Task Select_FetchesFullDocuments_ThenProjectsClientSide()
    {
        var (client, handler) = CreateClient(_ => """
            {"query":"*","result":[
                {"_id":"m1","_type":"movie","title":"Alien","releaseYear":1979},
                {"_id":"m2","_type":"movie","title":"Aliens","releaseYear":1986}
            ]}
            """);

        var titles = await client.Query<Movie>().OrderBy(m => m.ReleaseYear).Select(m => m.Title).ToListAsync();

        // The GROQ sent still fetches full documents -- Select doesn't
        // get pushed into the query, it's applied after the fact.
        Assert.Equal("""*[_type == "movie"] | order(releaseYear asc)""", handler.LastQuery);
        Assert.Equal(["Alien", "Aliens"], titles);
    }

    [Fact]
    public async Task CountAsync_TranslatesToGroqCount_NotAFetchAndClientCount()
    {
        var (client, handler) = CreateClient(_ => """{"query":"*","result":2}""");

        var count = await client.Query<Movie>().Where(m => m.ReleaseYear > 1970).CountAsync();

        Assert.Equal("""count(*[_type == "movie" && (releaseYear > 1970)])""", handler.LastQuery);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AnyAsync_TranslatesToGroqCountGreaterThanZero()
    {
        var (client, handler) = CreateClient(_ => """{"query":"*","result":true}""");

        var any = await client.Query<Movie>().Where(m => m.Title == "Alien").AnyAsync();

        Assert.Equal("""count(*[_type == "movie" && (title == "Alien")]) > 0""", handler.LastQuery);
        Assert.True(any);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_RequestsExactlyOneResult()
    {
        var (client, handler) = CreateClient(_ => """
            {"query":"*","result":[{"_id":"m1","_type":"movie","title":"Alien","releaseYear":1979}]}
            """);

        var movie = await client.Query<Movie>().OrderBy(m => m.ReleaseYear).FirstOrDefaultAsync();

        Assert.Equal("""*[_type == "movie"] | order(releaseYear asc)[0...1]""", handler.LastQuery);
        Assert.NotNull(movie);
        Assert.Equal("Alien", movie!.Title);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsNull_WhenNothingMatches()
    {
        var (client, _) = CreateClient(_ => """{"query":"*","result":[]}""");

        var movie = await client.Query<Movie>().FirstOrDefaultAsync();

        Assert.Null(movie);
    }

    [Fact]
    public async Task FirstAsync_Throws_WhenNothingMatches()
    {
        var (client, _) = CreateClient(_ => """{"query":"*","result":[]}""");

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.Query<Movie>().FirstAsync());
    }

    [Fact]
    public async Task SingleAsync_RequestsUpToTwoResults_ToDetectAmbiguity()
    {
        var (client, handler) = CreateClient(_ => """
            {"query":"*","result":[
                {"_id":"m1","_type":"movie","title":"Alien","releaseYear":1979},
                {"_id":"m2","_type":"movie","title":"Alien","releaseYear":1979}
            ]}
            """);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.Query<Movie>().Where(m => m.Title == "Alien").SingleAsync());
        Assert.Contains("more than one", ex.Message);
        Assert.Contains("[0...2]", handler.LastQuery);
    }

    [Fact]
    public async Task SingleOrDefaultAsync_ReturnsTheOneMatch()
    {
        var (client, _) = CreateClient(_ => """
            {"query":"*","result":[{"_id":"m1","_type":"movie","title":"Alien","releaseYear":1979}]}
            """);

        var movie = await client.Query<Movie>().SingleOrDefaultAsync();

        Assert.NotNull(movie);
        Assert.Equal("Alien", movie!.Title);
    }

    [Fact]
    public async Task SkipAndTake_ProduceTheExpectedGroqSlice()
    {
        var (client, handler) = CreateClient(_ => """{"query":"*","result":[]}""");

        await client.Query<Movie>().OrderBy(m => m.Title).Skip(20).Take(10).ToListAsync();

        Assert.Equal("""*[_type == "movie"] | order(title asc)[20...30]""", handler.LastQuery);
    }

    private sealed class Movie : SanityDocument
    {
        public string Title { get; set; } = string.Empty;
        public int ReleaseYear { get; set; }
    }

    /// <summary>A fake handler whose response is computed from the request (so Count/List/Any can return different shapes).</summary>
    private sealed class RoutedFakeHandler(Func<string, string> responder) : HttpMessageHandler
    {
        public string? LastQuery { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            var decoded = Uri.UnescapeDataString(uri.Split("query=", 2).ElementAtOrDefault(1)?.Split('&')[0] ?? string.Empty);
            LastQuery = decoded;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responder(decoded), Encoding.UTF8, "application/json"),
            };
        }
    }
}
