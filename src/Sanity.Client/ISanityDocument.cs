namespace Sanity.Client;

/// <summary>
/// Marker/contract for strongly-typed classes that represent a Sanity
/// document. Implement this directly, or inherit from
/// <see cref="SanityDocument"/> for a ready-made implementation, to get the
/// common system fields (<c>_id</c>, <c>_type</c>, <c>_rev</c>, timestamps)
/// populated automatically whenever the document is projected with those
/// fields included in your GROQ query (e.g. include them explicitly, or
/// use <c>...</c> to project everything).
/// </summary>
public interface ISanityDocument
{
    /// <summary>The document's unique id within the dataset (<c>_id</c>).</summary>
    string Id { get; set; }

    /// <summary>The document's schema type name (<c>_type</c>).</summary>
    string Type { get; set; }

    /// <summary>The current revision id (<c>_rev</c>), when projected.</summary>
    string? Rev { get; set; }

    /// <summary>UTC creation timestamp (<c>_createdAt</c>), when projected.</summary>
    DateTimeOffset? CreatedAt { get; set; }

    /// <summary>UTC last-updated timestamp (<c>_updatedAt</c>), when projected.</summary>
    DateTimeOffset? UpdatedAt { get; set; }
}
