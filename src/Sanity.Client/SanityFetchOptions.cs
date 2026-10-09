namespace Sanity.Client;

/// <summary>
/// Content Lake perspective to query against. Mirrors Sanity's
/// <c>perspective</c> query parameter.
/// </summary>
public enum SanityPerspective
{
    /// <summary>Only published documents (the default).</summary>
    Published,

    /// <summary>The raw/unfiltered perspective (returns drafts and versions too).</summary>
    Raw,

    /// <summary>Drafts take precedence over published documents where both exist.</summary>
    Drafts,
}

/// <summary>
/// Per-request options for <see cref="ISanityClient.FetchAsync{T}"/>.
/// </summary>
public sealed class SanityFetchOptions
{
    /// <summary>
    /// When set, requests Content Source Maps alongside the result
    /// (Sanity's <c>resultSourceMap</c> parameter). Required to drive
    /// stega encoding / click-to-edit via <c>Sanity.Client.Presentation</c>.
    /// Using <see cref="SanitySourceMapMode.WithKeyArraySelector"/> is
    /// recommended when the result will be stega-encoded, since it keeps
    /// array-item overlays stable when items are reordered.
    /// </summary>
    public SanitySourceMapMode ResultSourceMap { get; set; } = SanitySourceMapMode.None;

    /// <summary>
    /// The Content Lake perspective to query. Defaults to
    /// <see cref="SanityPerspective.Published"/> when left null, matching
    /// Sanity's own default.
    /// </summary>
    public SanityPerspective? Perspective { get; set; }
}

/// <summary>How Sanity should shape the returned Content Source Map, if any.</summary>
public enum SanitySourceMapMode
{
    /// <summary>Don't request a Content Source Map.</summary>
    None,

    /// <summary>Request a Content Source Map with numeric array-item selectors.</summary>
    Enabled,

    /// <summary>
    /// Request a Content Source Map using stable <c>_key</c>-based
    /// selectors for array items instead of numeric indices.
    /// </summary>
    WithKeyArraySelector,
}
