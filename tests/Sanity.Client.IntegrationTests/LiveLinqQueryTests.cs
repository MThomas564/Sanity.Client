using Sanity.Client.Linq;

namespace Sanity.Client.IntegrationTests;

/// <summary>
/// Real network tests for the LINQ query provider against Sanity's
/// public demo dataset (<c>ppsg7ml5</c> / <c>movies</c>). Deliberately
/// NOT part of the PR/merge gate -- see
/// .github/workflows/integration.yml.
/// </summary>
public sealed class LiveLinqQueryTests
{
    private static ISanityClient CreateClient() => new SanityClient(
        new HttpClient(),
        new SanityClientOptions
        {
            ProjectId = "ppsg7ml5",
            Dataset = "movies",
            ApiVersion = "v2024-05-07",
        });

    [Fact]
    public async Task Where_OrderBy_Take_ReturnsRealMovies()
    {
        var client = CreateClient();

        var movies = await client.Query<Movie>()
            .Where(m => m.Title != null)
            .OrderBy(m => m.Title)
            .Take(3)
            .ToListAsync();

        Assert.NotEmpty(movies);
        Assert.True(movies.Count <= 3);
        Assert.All(movies, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }

    [Fact]
    public async Task Select_ProjectsToTitlesOnly()
    {
        var client = CreateClient();

        var titles = await client.Query<Movie>()
            .OrderBy(m => m.Title)
            .Take(3)
            .Select(m => m.Title)
            .ToListAsync();

        Assert.NotEmpty(titles);
        Assert.All(titles, Assert.NotNull);
    }

    [Fact]
    public async Task CountAsync_ReturnsARealCount()
    {
        var client = CreateClient();

        var count = await client.Query<Movie>().CountAsync();

        Assert.True(count > 0, "the public movies dataset should have at least one document");
    }

    [Fact]
    public async Task AnyAsync_WithPredicate_MatchesANonExistentTitle_ReturnsFalse()
    {
        var client = CreateClient();

        var any = await client.Query<Movie>()
            .AnyAsync(m => m.Title == "Definitely Not A Real Movie Title 12345");

        Assert.False(any);
    }

    [Fact]
    public async Task FirstOrDefaultAsync_ReturnsADocument()
    {
        var client = CreateClient();

        var movie = await client.Query<Movie>().OrderBy(m => m.Title).FirstOrDefaultAsync();

        Assert.NotNull(movie);
    }

    [SanityType("movie")]
    private sealed class Movie : SanityDocument
    {
        public string? Title { get; set; }
    }
}
