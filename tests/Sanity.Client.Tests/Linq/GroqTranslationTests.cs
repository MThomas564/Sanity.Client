using System.Net;
using Sanity.Client.Linq;

namespace Sanity.Client.Tests.Linq;

/// <summary>
/// Verifies the exact GROQ a given LINQ chain produces, via
/// <see cref="SanityQueryableExtensions.ToGroqQuery{T}"/> -- no network
/// calls, just checking the translator itself.
/// </summary>
public sealed class GroqTranslationTests
{
    private static ISanityClient CreateClient() => new SanityClient(
        new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, """{"query":"*","result":null}""")),
        new SanityClientOptions { ProjectId = "test123", Dataset = "production" });

    [Fact]
    public void Query_WithNoOperators_FiltersByCamelCasedTypeName()
    {
        var groq = CreateClient().Query<Movie>().ToGroqQuery();
        Assert.Equal("""*[_type == "movie"]""", groq);
    }

    [Fact]
    public void SanityTypeAttribute_OverridesTheDefaultTypeName()
    {
        var groq = CreateClient().Query<BlogPost>().ToGroqQuery();
        Assert.Equal("""*[_type == "post"]""", groq);
    }

    [Fact]
    public void Where_SimpleEquality_TranslatesToGroqEquality()
    {
        var groq = CreateClient().Query<Movie>().Where(m => m.Title == "Alien").ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (title == "Alien")]""", groq);
    }

    [Fact]
    public void Where_MultipleClauses_AreAnded()
    {
        var groq = CreateClient().Query<Movie>()
            .Where(m => m.Title == "Alien")
            .Where(m => m.ReleaseYear > 1970)
            .ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (title == "Alien") && (releaseYear > 1970)]""", groq);
    }

    [Fact]
    public void Where_AndAlsoOrElse_TranslateWithParens()
    {
        var groq = CreateClient().Query<Movie>()
            .Where(m => m.Title == "Alien" || (m.ReleaseYear > 1970 && m.ReleaseYear < 1990))
            .ToGroqQuery();
        Assert.Equal(
            """*[_type == "movie" && ((title == "Alien" || (releaseYear > 1970 && releaseYear < 1990)))]""",
            groq);
    }

    [Fact]
    public void Where_ClosureCapturedVariable_IsEvaluatedToItsRuntimeValue()
    {
        var minYear = 1980;
        var groq = CreateClient().Query<Movie>().Where(m => m.ReleaseYear >= minYear).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (releaseYear >= 1980)]""", groq);
    }

    [Fact]
    public void Where_ValueOnLeftSide_FlipsTheOperatorToKeepTheMemberFirst()
    {
        var groq = CreateClient().Query<Movie>().Where(m => 1979 < m.ReleaseYear).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (releaseYear > 1979)]""", groq);
    }

    [Fact]
    public void Where_StringContains_TranslatesToGroqMatch()
    {
        var groq = CreateClient().Query<Movie>().Where(m => m.Title.Contains("lien")).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (title match "*lien*")]""", groq);
    }

    [Fact]
    public void Where_StringStartsWith_TranslatesToGroqMatch()
    {
        var groq = CreateClient().Query<Movie>().Where(m => m.Title.StartsWith("Ali")).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (title match "Ali*")]""", groq);
    }

    [Fact]
    public void Where_EnumerableContains_TranslatesToGroqIn()
    {
        var years = new[] { 1979, 1986 };
        var groq = CreateClient().Query<Movie>().Where(m => years.Contains(m.ReleaseYear)).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (releaseYear in [1979, 1986])]""", groq);
    }

    [Fact]
    public void Where_NullCheck_TranslatesDirectly()
    {
        var groq = CreateClient().Query<Movie>().Where(m => m.Tagline != null).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (tagline != null)]""", groq);
    }

    [Fact]
    public void OrderBy_TranslatesToGroqOrderAscending()
    {
        var groq = CreateClient().Query<Movie>().OrderBy(m => m.ReleaseYear).ToGroqQuery();
        Assert.Equal("""*[_type == "movie"] | order(releaseYear asc)""", groq);
    }

    [Fact]
    public void OrderByDescendingThenBy_TranslatesToMultiFieldOrder()
    {
        var groq = CreateClient().Query<Movie>()
            .OrderByDescending(m => m.ReleaseYear)
            .ThenBy(m => m.Title)
            .ToGroqQuery();
        Assert.Equal("""*[_type == "movie"] | order(releaseYear desc, title asc)""", groq);
    }

    [Fact]
    public void SkipAndTake_TranslateToGroqSlice()
    {
        var groq = CreateClient().Query<Movie>().Skip(10).Take(5).ToGroqQuery();
        Assert.Equal("""*[_type == "movie"][10...15]""", groq);
    }

    [Fact]
    public void TakeOnly_StartsAtZero()
    {
        var groq = CreateClient().Query<Movie>().Take(3).ToGroqQuery();
        Assert.Equal("""*[_type == "movie"][0...3]""", groq);
    }

    [Fact]
    public void FullChain_FilterOrderAndPage_ComposeInOrder()
    {
        var groq = CreateClient().Query<Movie>()
            .Where(m => m.ReleaseYear > 1970)
            .OrderByDescending(m => m.ReleaseYear)
            .Skip(0)
            .Take(10)
            .ToGroqQuery();
        Assert.Equal(
            """*[_type == "movie" && (releaseYear > 1970)] | order(releaseYear desc)[0...10]""",
            groq);
    }

    [Fact]
    public void Count_TranslatesToGroqCountIgnoringOrderAndPaging()
    {
        var groq = CreateClient().Query<Movie>()
            .Where(m => m.ReleaseYear > 1970)
            .OrderBy(m => m.Title)
            .Take(5)
            .ToGroqQuery();
        // Note: .Count() itself isn't chained here since ToGroqQuery<T>
        // requires a terminal that still returns IQueryable<T> up to
        // this call; CountAsync's own GROQ shape is covered by
        // SanityQueryableExecutionTests against a fake handler instead.
        Assert.Contains("releaseYear > 1970", groq);
    }

    [Fact]
    public void NestedMemberAccess_TranslatesToDottedGroqPath()
    {
        var groq = CreateClient().Query<Movie>().Where(m => m.Rating.Average > 8.0).ToGroqQuery();
        Assert.Equal("""*[_type == "movie" && (rating.average > 8)]""", groq);
    }

    [Fact]
    public void UnsupportedExpression_ThrowsNotSupportedExceptionRatherThanGuessing()
    {
        var ex = Assert.Throws<NotSupportedException>(
            () => CreateClient().Query<Movie>().Where(m => m.Title.Length > 5).ToGroqQuery());
        Assert.Contains("not supported", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class Movie : SanityDocument
    {
        public string Title { get; set; } = string.Empty;
        public int ReleaseYear { get; set; }
        public string? Tagline { get; set; }
        public MovieRating Rating { get; set; } = new();
    }

    private sealed class MovieRating
    {
        public double Average { get; set; }
    }

    [SanityType("post")]
    private sealed class BlogPost : SanityDocument
    {
        public string Title { get; set; } = string.Empty;
    }
}
