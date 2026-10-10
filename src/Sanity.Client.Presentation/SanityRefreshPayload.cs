using System.Text.Json.Serialization;

namespace Sanity.Client.Presentation;

/// <summary>
/// The payload passed to your refresh callback (see
/// <see cref="ISanityPresentationService.EnableVisualEditingAsync"/>) when
/// the Presentation tool reports that content changed and your preview
/// should update.
/// </summary>
public sealed class SanityRefreshPayload
{
    /// <summary>
    /// What triggered the refresh: <c>"mutation"</c> when a document was
    /// edited in the Studio, <c>"manual"</c> when the editor clicked the
    /// refresh button in the Studio, or another source defined by future
    /// versions of the Presentation tool.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// The document that changed. Only present when <see cref="Source"/>
    /// is <c>"mutation"</c>.
    /// </summary>
    public SanityRefreshDocument? Document { get; set; }
}

/// <summary>Identifies the document a <see cref="SanityRefreshPayload"/> is about.</summary>
public sealed class SanityRefreshDocument
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("_type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("_rev")]
    public string? Rev { get; set; }
}
