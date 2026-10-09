namespace Sanity.Client.BlazorSample.Models;

/// <summary>
/// Projection shape queried from Sanity's public "movies" demo dataset
/// (project "ppsg7ml5"), used by the Movies page.
///
/// Inheriting from <see cref="SanityDocument"/> picks up the common system
/// fields (<c>_id</c>, <c>_type</c>, <c>_rev</c>, timestamps) automatically
/// whenever they're included in the GROQ projection -- only the
/// movie-specific fields need to be declared here.
/// </summary>
public sealed class Movie : SanityDocument
{
    public string Title { get; set; } = string.Empty;

    public double? Popularity { get; set; }

    public DateTimeOffset? ReleaseDate { get; set; }

    public string? PosterUrl { get; set; }
}
