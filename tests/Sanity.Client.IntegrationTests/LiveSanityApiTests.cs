using Sanity.Client;

namespace Sanity.Client.IntegrationTests;

/// <summary>
/// Real network tests against Sanity's public demo dataset
/// (<c>ppsg7ml5</c> / <c>movies</c>, no auth required -- used throughout
/// the sample app too). Deliberately NOT part of the PR/merge gate: run
/// only on a schedule or via workflow_dispatch (see
/// .github/workflows/integration.yml), since network flakiness here
/// would otherwise block unrelated PRs.
/// </summary>
public sealed class LiveSanityApiTests
{
    private static SanityClient CreateClient() => new(
        new HttpClient(),
        new SanityClientOptions
        {
            ProjectId = "ppsg7ml5",
            Dataset = "movies",
            ApiVersion = "v2024-05-07",
        });

    [Fact]
    public async Task FetchAsync_ReturnsRealMovies_ViaGet()
    {
        var client = CreateClient();

        var result = await client.FetchAsync<List<MovieTitle>>(
            "*[_type == \"movie\"][0...3]{title}");

        Assert.NotNull(result.Result);
        Assert.NotEmpty(result.Result!);
        Assert.All(result.Result!, m => Assert.False(string.IsNullOrWhiteSpace(m.Title)));
    }

    [Fact]
    public async Task FetchAsync_FallsBackToPost_AgainstRealApi_ForVeryLongQueries()
    {
        var client = CreateClient();

        // Padded well past the 11,264-character GET threshold -- this is
        // the live-API counterpart of the fake-handler POST fallback
        // tests in Sanity.Client.Tests; it exercises the real HTTP path
        // (real headers, real chunking/transfer behavior) that a fake
        // handler can't.
        var padding = string.Join(
            " || ",
            Enumerable.Range(0, 1200).Select(i => $"title == \"not-a-real-title-{i}\""));
        var longQuery = $"*[_type == \"movie\" && ({padding})][0...3]{{title}}";
        Assert.True(longQuery.Length > 11264, "test query should exceed the GET threshold");

        var result = await client.FetchAsync<List<MovieTitle>>(longQuery);

        // None of the padding titles are real, so an empty result is
        // the correct, successful outcome -- the thing under test is
        // that the request round-trips at all (i.e. actually went via
        // POST and the real API accepted it), not what it returns.
        Assert.NotNull(result.Result);
        Assert.Empty(result.Result!);
    }

    [Fact]
    public async Task FetchAsync_DraftsPerspective_SucceedsAgainstTheRealApi_ViaTheNonCdnHost()
    {
        // The default client (no token, UseCdn's default of true) would
        // normally route to apicdn.sanity.io, which hard-rejects the
        // perspective param entirely for anything but published. This is
        // the live-API confirmation that SanityClient correctly routes a
        // Drafts/Raw request to the non-CDN host regardless, instead of
        // surfacing that CDN error to every caller who forgets to also
        // set UseCdn = false by hand.
        var client = CreateClient();

        var result = await client.FetchAsync<MovieTitle>(
            "*[_type == \"movie\"][0]{title}",
            options: new SanityFetchOptions { Perspective = SanityPerspective.Drafts });

        Assert.NotNull(result.Result);
        Assert.False(string.IsNullOrWhiteSpace(result.Result!.Title));
    }

    private sealed class MovieTitle
    {
        public string Title { get; set; } = string.Empty;
    }
}
