using System.Text.Json.Serialization;

namespace Sanity.Client;

/// <summary>
/// Optional convenience base class for strongly-typed documents. Inherit
/// from this in your own model to automatically map Sanity's system fields
/// (<c>_id</c>, <c>_type</c>, <c>_rev</c>, <c>_createdAt</c>,
/// <c>_updatedAt</c>) without having to add the <see cref="JsonPropertyNameAttribute"/>
/// mappings yourself:
///
/// <code>
/// public sealed class Post : SanityDocument
/// {
///     public string Title { get; set; } = string.Empty;
///     public string? Body { get; set; }
/// }
///
/// var posts = await sanity.FetchResultAsync&lt;Post[]&gt;(
///     "*[_type == \"post\"]{_id, _type, title, body}");
/// </code>
///
/// This is purely a convenience: any plain class (with or without these
/// fields, and with or without implementing <see cref="ISanityDocument"/>)
/// works fine as the generic type argument to
/// <see cref="ISanityClient.FetchAsync{T}"/> / <see cref="ISanityClient.FetchResultAsync{T}"/>.
/// </summary>
public abstract class SanityDocument : ISanityDocument
{
    /// <inheritdoc />
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("_type")]
    public string Type { get; set; } = string.Empty;

    /// <inheritdoc />
    [JsonPropertyName("_rev")]
    public string? Rev { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("_createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    /// <inheritdoc />
    [JsonPropertyName("_updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}
