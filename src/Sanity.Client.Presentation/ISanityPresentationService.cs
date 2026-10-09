namespace Sanity.Client.Presentation;

/// <summary>
/// Blazor-side support for Sanity's Presentation tool: stega-encoding
/// query results for click-to-edit, and enabling the overlay script that
/// talks to the Studio when your app is rendered inside the Presentation
/// iframe. All required JavaScript is vendored inside this package's
/// static web assets -- nothing is loaded from a third-party CDN at
/// runtime.
/// </summary>
public interface ISanityPresentationService : IAsyncDisposable
{
    /// <summary>
    /// Stega-encodes the string values of <paramref name="result"/> using
    /// the <paramref name="resultSourceMap"/> returned alongside it
    /// (request one via <c>SanityFetchOptions.ResultSourceMap</c>). Returns
    /// a new instance of <typeparamref name="T"/> with encoded strings;
    /// the input is not mutated. If <paramref name="resultSourceMap"/> is
    /// null, <paramref name="result"/> is returned unchanged.
    /// </summary>
    ValueTask<T> StegaEncodeAsync<T>(
        T result,
        System.Text.Json.JsonElement? resultSourceMap,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Strips stega-encoded metadata back out of a string, if present.
    /// Safe to call on any string, encoded or not.
    /// </summary>
    ValueTask<string> StegaCleanAsync(string value, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads <c>@sanity/visual-editing-standalone</c> (vendored locally)
    /// and enables click-to-edit overlays on the current page. Call this
    /// only while in "preview"/draft mode -- keep it out of your
    /// production render path entirely, per Sanity's guidance, by gating
    /// it behind whatever draft-mode flag your app uses. Returns an
    /// <see cref="IAsyncDisposable"/>; dispose it (or call
    /// <see cref="DisableVisualEditingAsync"/>) to remove the overlays.
    /// </summary>
    Task<IAsyncDisposable> EnableVisualEditingAsync(CancellationToken cancellationToken = default);

    /// <summary>Disables overlays previously enabled on this service instance, if any.</summary>
    Task DisableVisualEditingAsync(CancellationToken cancellationToken = default);
}
