using System.Linq.Expressions;

namespace Sanity.Client.Linq;

/// <summary>
/// LINQ querying for Sanity: translates a subset of standard
/// <see cref="Queryable"/> expressions (<c>Where</c>, <c>Select</c>,
/// <c>OrderBy</c>/<c>OrderByDescending</c>/<c>ThenBy</c>/<c>ThenByDescending</c>,
/// <c>Skip</c>/<c>Take</c>, <c>Count</c>, <c>First(OrDefault)</c>,
/// <c>Single(OrDefault)</c>, <c>Any</c>) into GROQ, instead of hand-writing
/// query strings. Read-only -- there is no LINQ surface for mutations.
///
/// See the "Supported LINQ operators" section of the Sanity.Client
/// README for exactly what is and isn't translated; an unsupported
/// expression throws <see cref="NotSupportedException"/> rather than
/// silently producing a wrong query.
/// </summary>
public static class SanityQueryableExtensions
{
    /// <summary>
    /// Starts a LINQ query over all documents of type <typeparamref name="T"/>
    /// (filtered to <c>_type == "&lt;type name&gt;"</c>, derived from
    /// <typeparamref name="T"/>'s name or a <see cref="SanityTypeAttribute"/>
    /// override). Nothing is sent over the network until you call one of
    /// the <c>...Async</c> terminal methods below (or, discouraged,
    /// enumerate synchronously).
    /// </summary>
    public static IQueryable<T> Query<T>(this ISanityClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new SanityQueryable<T>(new SanityQueryProvider(client));
    }

    /// <summary>Translates the query to GROQ without executing it -- useful for debugging/logging.</summary>
    public static string ToGroqQuery<T>(this IQueryable<T> source)
    {
        var translation = SanityExpressionWalker.Parse(RequireSanityExpression(source));
        return translation.Mode switch
        {
            SanityQueryMode.Count or SanityQueryMode.LongCount => translation.BuildCountQuery(),
            SanityQueryMode.Any => $"{translation.BuildCountQuery()} > 0",
            _ => translation.BuildGroqQuery(),
        };
    }

    public static Task<List<T>> ToListAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        GetProvider(source).ExecuteAsync<List<T>>(RequireSanityExpression(source), cancellationToken);

    public static Task<int> CountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, int>(source, q => q.Count(), cancellationToken);

    public static Task<long> LongCountAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, long>(source, q => q.LongCount(), cancellationToken);

    public static Task<bool> AnyAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, bool>(source, q => q.Any(), cancellationToken);

    public static Task<bool> AnyAsync<T>(
        this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Execute<T, bool>(source, q => q.Any(predicate), cancellationToken);

    public static Task<T?> FirstOrDefaultAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, T?>(source, q => q.FirstOrDefault(), cancellationToken);

    public static Task<T?> FirstOrDefaultAsync<T>(
        this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Execute<T, T?>(source, q => q.FirstOrDefault(predicate), cancellationToken);

    public static Task<T> FirstAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, T>(source, q => q.First(), cancellationToken);

    public static Task<T> FirstAsync<T>(
        this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Execute<T, T>(source, q => q.First(predicate), cancellationToken);

    public static Task<T?> SingleOrDefaultAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, T?>(source, q => q.SingleOrDefault(), cancellationToken);

    public static Task<T?> SingleOrDefaultAsync<T>(
        this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Execute<T, T?>(source, q => q.SingleOrDefault(predicate), cancellationToken);

    public static Task<T> SingleAsync<T>(this IQueryable<T> source, CancellationToken cancellationToken = default) =>
        Execute<T, T>(source, q => q.Single(), cancellationToken);

    public static Task<T> SingleAsync<T>(
        this IQueryable<T> source, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default) =>
        Execute<T, T>(source, q => q.Single(predicate), cancellationToken);

    /// <summary>
    /// Builds the terminal expression the same way the compiler would for
    /// the equivalent synchronous call (<c>q.FirstOrDefault()</c> etc.) by
    /// actually invoking it against a throwaway <see cref="IQueryable{T}"/>
    /// facade that just records the expression instead of executing --
    /// this keeps every terminal operator's expression-tree shape
    /// (method, generic arguments, quoted lambda) byte-for-byte identical
    /// to what <see cref="Queryable"/> itself would produce, instead of
    /// this library hand-building `MethodCallExpression`s that could
    /// subtly drift from what the walker expects.
    /// </summary>
    private static Task<TResult> Execute<T, TResult>(
        IQueryable<T> source, Func<IQueryable<T>, TResult> buildTerminalCall, CancellationToken cancellationToken)
    {
        var provider = GetProvider(source);
        var expression = RequireSanityExpression(source);
        var recorder = new ExpressionRecordingQueryable<T>(expression);
        buildTerminalCall(recorder);
        return provider.ExecuteAsync<TResult>(recorder.RecordedExpression!, cancellationToken);
    }

    private static SanityQueryProvider GetProvider<T>(IQueryable<T> source) =>
        source.Provider as SanityQueryProvider
            ?? throw new ArgumentException(
                $"'{nameof(source)}' is not a Sanity LINQ query -- it must originate from {nameof(ISanityClient)}.{nameof(Query)}<T>().",
                nameof(source));

    private static Expression RequireSanityExpression<T>(IQueryable<T> source)
    {
        GetProvider(source); // validates it's ours; throws a clear error otherwise.
        return source.Expression;
    }

    /// <summary>
    /// A minimal <see cref="IQueryable{T}"/>/<see cref="IQueryProvider"/>
    /// pair whose only job is to capture the <see cref="Expression"/> a
    /// <see cref="Queryable"/> terminal method builds when called against
    /// it, rather than execute anything.
    /// </summary>
    private sealed class ExpressionRecordingQueryable<T>(Expression baseExpression) : IQueryable<T>, IQueryProvider
    {
        public Expression? RecordedExpression { get; private set; }

        public Type ElementType => typeof(T);

        public Expression Expression => baseExpression;

        public IQueryProvider Provider => this;

        public IQueryable CreateQuery(Expression expression)
        {
            RecordedExpression = expression;
            return this;
        }

        public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            RecordedExpression = expression;
            return (IQueryable<TElement>)(object)this;
        }

        public object? Execute(Expression expression)
        {
            RecordedExpression = expression;
            return null;
        }

        public TResult Execute<TResult>(Expression expression)
        {
            RecordedExpression = expression;
            return default!;
        }

        public IEnumerator<T> GetEnumerator() => Enumerable.Empty<T>().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
