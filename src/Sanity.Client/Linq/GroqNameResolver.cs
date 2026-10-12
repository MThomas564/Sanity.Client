using System.Reflection;
using System.Text.Json.Serialization;

namespace Sanity.Client.Linq;

/// <summary>
/// Maps C# type/member names to the GROQ field names Sanity actually
/// stores, using the same convention <see cref="SanityClient"/> already
/// uses for JSON deserialization (camelCase, honoring
/// <see cref="JsonPropertyNameAttribute"/>) -- so a LINQ query and a
/// hand-written GROQ query against the same model always agree on field
/// names.
/// </summary>
internal static class GroqNameResolver
{
    /// <summary>
    /// The GROQ <c>_type</c> filter value for <paramref name="type"/>:
    /// <see cref="SanityTypeAttribute"/> if present, else the camelCased
    /// class name.
    /// </summary>
    public static string ResolveTypeName(Type type)
    {
        var attribute = type.GetCustomAttribute<SanityTypeAttribute>();
        return attribute?.Name ?? ToCamelCase(type.Name);
    }

    /// <summary>
    /// The GROQ field name for a property: its
    /// <see cref="JsonPropertyNameAttribute"/> value if present, else the
    /// camelCased property name.
    /// </summary>
    public static string ResolveFieldName(MemberInfo member)
    {
        var attribute = member.GetCustomAttribute<JsonPropertyNameAttribute>();
        return attribute?.Name ?? ToCamelCase(member.Name);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        // Matches System.Text.Json's own JsonNamingPolicy.CamelCase
        // behavior closely enough for typical PascalCase .NET property
        // names (lowercase the first char only; "ID" -> "iD" is the one
        // known quirk both share).
        return string.Create(name.Length, name, static (span, source) =>
        {
            source.AsSpan().CopyTo(span);
            span[0] = char.ToLowerInvariant(span[0]);
        });
    }
}
