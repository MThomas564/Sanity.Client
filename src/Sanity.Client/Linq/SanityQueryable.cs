using System.Collections;
using System.Linq.Expressions;

namespace Sanity.Client.Linq;

/// <summary>
/// A real <see cref="IQueryable{T}"/> over a Sanity document type: every
/// standard <see cref="System.Linq.Queryable"/> extension method you
/// chain onto it (<c>Where</c>, <c>Select</c>, <c>OrderBy</c>, <c>Skip</c>,
/// <c>Take</c>, ...) composes an expression tree exactly like it would
/// for any other LINQ provider (e.g. EF Core) -- it's only actually
/// translated to GROQ and executed when you call one of the async
/// terminal operators (<see cref="SanityQueryableExtensions.ToListAsync{T}"/>
/// etc.), or (discouraged -- see those docs) enumerate it synchronously.
/// </summary>
internal sealed class SanityQueryable<T> : IOrderedQueryable<T>, ISanityQueryRoot
{
    private readonly SanityQueryProvider _provider;
    private readonly Expression _expression;

    /// <summary>Constructs the root of a query (before any Where/Select/... is applied).</summary>
    public SanityQueryable(SanityQueryProvider provider)
    {
        _provider = provider;
        _expression = Expression.Constant(this);
    }

    /// <summary>Constructs a composed query (the result of CreateQuery after chaining a LINQ operator).</summary>
    public SanityQueryable(SanityQueryProvider provider, Expression expression)
    {
        _provider = provider;
        _expression = expression;
    }

    public Type ElementType => typeof(T);

    Type IQueryable.ElementType => typeof(T);

    public Expression Expression => _expression;

    public IQueryProvider Provider => _provider;

    /// <summary>
    /// Blocking synchronous enumeration -- executes the query and blocks
    /// on the HTTP call. Prefer
    /// <see cref="SanityQueryableExtensions.ToListAsync{T}"/>; this exists
    /// only so plain <c>foreach</c> and the synchronous LINQ operators
    /// (<c>.ToList()</c>, <c>.Count()</c>, etc., as opposed to this
    /// library's own <c>...Async</c> methods) work at all, matching how
    /// every other synchronous <see cref="IQueryable{T}"/> consumer
    /// expects to be able to use one.
    /// </summary>
    public IEnumerator<T> GetEnumerator() =>
        _provider.ExecuteAsync<List<T>>(_expression, CancellationToken.None)
            .GetAwaiter().GetResult().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
