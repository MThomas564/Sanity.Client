namespace Sanity.Client.Linq;

/// <summary>
/// Overrides the GROQ <c>_type</c> value used to filter documents for
/// this class when queried via <see cref="SanityQueryableExtensions.Query{T}"/>.
/// Without this attribute, the type filter defaults to the camelCased
/// class name (e.g. <c>BlogPost</c> -> <c>"blogPost"</c>), matching
/// Sanity's own schema-type naming convention.
/// </summary>
/// <example>
/// <code>
/// [SanityType("post")]
/// public sealed class Post : SanityDocument { ... }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class SanityTypeAttribute : Attribute
{
    public SanityTypeAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A GROQ _type value is required.", nameof(name));
        }

        Name = name;
    }

    /// <summary>The GROQ <c>_type</c> string value, e.g. <c>"post"</c>.</summary>
    public string Name { get; }
}
