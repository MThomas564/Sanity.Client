namespace Sanity.Client.Presentation;

/// <summary>
/// Options controlling how overlays and stega-encoded click-to-edit links
/// are generated.
/// </summary>
public sealed class SanityPresentationOptions
{
    public const string SectionName = "SanityPresentation";

    /// <summary>
    /// The base URL of your Sanity Studio (e.g.
    /// <c>https://my-project.sanity.studio</c>, or <c>/studio</c> if it's
    /// mounted on the same origin as your frontend). Required to build
    /// click-to-edit links.
    /// </summary>
    public string? StudioUrl { get; set; }

    /// <summary>
    /// When true, cross-dataset reference metadata (<c>_projectId</c> /
    /// <c>_dataset</c>) is omitted from generated edit links. Defaults to
    /// false.
    /// </summary>
    public bool OmitCrossDatasetReferenceData { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(StudioUrl))
        {
            throw new InvalidOperationException(
                $"{nameof(SanityPresentationOptions)}.{nameof(StudioUrl)} must be configured.");
        }
    }
}
