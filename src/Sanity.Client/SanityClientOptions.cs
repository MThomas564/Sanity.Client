namespace Sanity.Client;

/// <summary>
/// Configuration options for connecting to a Sanity.io project's Content Lake.
/// </summary>
public sealed class SanityClientOptions
{
    /// <summary>
    /// The section name used when binding these options from configuration
    /// (e.g. appsettings.json: <c>"Sanity": { ... }</c>).
    /// </summary>
    public const string SectionName = "Sanity";

    /// <summary>
    /// The Sanity project ID, e.g. "abc123de". Required.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The dataset name, e.g. "production". Required.
    /// </summary>
    public string Dataset { get; set; } = "production";

    /// <summary>
    /// The Sanity API version to target, in "vYYYY-MM-DD" form, or "v1".
    /// Defaults to a recent stable date-based version.
    /// </summary>
    public string ApiVersion { get; set; } = "v2024-01-01";

    /// <summary>
    /// Optional API token (Bearer) used for authenticated requests, required
    /// for private datasets. Not needed for public read-only datasets.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Whether to query through Sanity's fast, eventually-consistent CDN
    /// (apicdn.sanity.io) rather than the strongly-consistent API
    /// (api.sanity.io). Defaults to true. Automatically disabled when a
    /// token is supplied with <see cref="UseCdnWithToken"/> left false,
    /// since authenticated requests commonly need fresh data.
    /// </summary>
    public bool UseCdn { get; set; } = true;

    /// <summary>
    /// When a <see cref="Token"/> is set, whether to still allow routing
    /// through the CDN. Defaults to false (authenticated requests bypass
    /// the CDN for consistency) unless explicitly opted in.
    /// </summary>
    public bool UseCdnWithToken { get; set; }

    /// <summary>
    /// Optional override for the API host template. Defaults to
    /// Sanity's standard "{0}.api.sanity.io" / "{0}.apicdn.sanity.io" hosts.
    /// Mainly useful for testing against a mock server.
    /// </summary>
    public Uri? BaseUriOverride { get; set; }

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectId))
        {
            throw new InvalidOperationException(
                $"{nameof(SanityClientOptions)}.{nameof(ProjectId)} must be configured.");
        }

        if (string.IsNullOrWhiteSpace(Dataset))
        {
            throw new InvalidOperationException(
                $"{nameof(SanityClientOptions)}.{nameof(Dataset)} must be configured.");
        }

        if (string.IsNullOrWhiteSpace(ApiVersion))
        {
            throw new InvalidOperationException(
                $"{nameof(SanityClientOptions)}.{nameof(ApiVersion)} must be configured.");
        }
    }

    internal bool EffectiveUseCdn => UseCdn && (string.IsNullOrEmpty(Token) || UseCdnWithToken);

    internal Uri BuildBaseUri()
    {
        if (BaseUriOverride is not null)
        {
            return BaseUriOverride;
        }

        var host = EffectiveUseCdn
            ? $"{ProjectId}.apicdn.sanity.io"
            : $"{ProjectId}.api.sanity.io";

        return new Uri($"https://{host}/");
    }
}
