using System.Linq.Expressions;

namespace Sanity.Client.Linq;

internal enum SanityQueryMode
{
    List,
    Count,
    LongCount,
    First,
    FirstOrDefault,
    Single,
    SingleOrDefault,
    Any,
}

internal sealed record SanityOrderClause(string Field, bool Descending);

/// <summary>
/// Accumulated, mutable state built up by <see cref="SanityExpressionWalker"/>
/// as it walks a composed <c>Queryable</c> expression tree
/// (<c>Where(OrderBy(Skip(source, ...), ...), ...)</c> etc.), then turned
/// into an actual GROQ query string by <see cref="BuildGroqQuery"/>.
/// </summary>
internal sealed class SanityQueryTranslation
{
    public required Type ElementType { get; init; }

    public required string TypeName { get; init; }

    public List<string> WhereFragments { get; } = [];

    public List<SanityOrderClause> OrderClauses { get; } = [];

    public int? SkipCount { get; set; }

    public int? TakeCount { get; set; }

    public SanityQueryMode Mode { get; set; } = SanityQueryMode.List;

    /// <summary>
    /// Set when the query ends in <c>.Select(...)</c>. Applied client-side
    /// against the fetched <see cref="ElementType"/> results -- this
    /// query's GROQ always fetches the full documents of
    /// <see cref="ElementType"/>; no projection is pushed into GROQ
    /// itself (yet -- see the README's "Supported LINQ operators"
    /// section for why, and what that costs you).
    /// </summary>
    public LambdaExpression? Projector { get; set; }

    /// <summary>
    /// Builds the GROQ query for fetching the base result set as
    /// <see cref="ElementType"/> (i.e. BEFORE any client-side
    /// <see cref="Projector"/> is applied, and before Count/First/etc.
    /// post-processing -- those are handled by the caller based on
    /// <see cref="Mode"/>).
    /// </summary>
    public string BuildGroqQuery(int? takeOverride = null)
    {
        var filter = $"_type == {GroqExpressionTranslator.TranslateConstant(TypeName)}";
        foreach (var fragment in WhereFragments)
        {
            filter += $" && ({fragment})";
        }

        var query = $"*[{filter}]";

        if (OrderClauses.Count > 0)
        {
            var orderings = OrderClauses.Select(o => $"{o.Field} {(o.Descending ? "desc" : "asc")}");
            query += $" | order({string.Join(", ", orderings)})";
        }

        var effectiveTake = takeOverride ?? TakeCount;
        if (SkipCount is not null || effectiveTake is not null)
        {
            var start = SkipCount ?? 0;
            if (effectiveTake is { } take)
            {
                query += $"[{start}...{start + take}]";
            }
            else
            {
                // Skip with no Take: GROQ has no open-ended slice syntax,
                // so request a very large upper bound instead. (Sanity's
                // own practical result-size limits make this safe --
                // there's no dataset where "skip N, take the rest" needs
                // more than this.)
                query += $"[{start}...{start + 1_000_000}]";
            }
        }

        return query;
    }

    /// <summary>GROQ to count the filtered set, ignoring ordering/paging (which don't affect a count).</summary>
    public string BuildCountQuery()
    {
        var filter = $"_type == {GroqExpressionTranslator.TranslateConstant(TypeName)}";
        foreach (var fragment in WhereFragments)
        {
            filter += $" && ({fragment})";
        }

        return $"count(*[{filter}])";
    }
}
