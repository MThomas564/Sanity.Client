using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace Sanity.Client.Linq;

/// <summary>
/// The <see cref="IQueryProvider"/> backing <see cref="SanityQueryable{T}"/>.
/// Synchronous <see cref="Execute{TResult}"/>/<see cref="Execute"/> exist
/// only to satisfy the <see cref="IQueryProvider"/> contract (so plain
/// synchronous LINQ and <c>foreach</c> work); real callers should use
/// <see cref="ExecuteAsync{TResult}"/> via the <c>...Async</c> extension
/// methods instead of blocking on an HTTP call.
/// </summary>
internal sealed class SanityQueryProvider : IQueryProvider
{
    private static readonly MethodInfo FetchResultAsyncMethod =
        typeof(ISanityClient).GetMethod(nameof(ISanityClient.FetchResultAsync))!;

    private readonly ISanityClient _client;

    public SanityQueryProvider(ISanityClient client) => _client = client;

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = GetSequenceElementType(expression.Type);
        var queryableType = typeof(SanityQueryable<>).MakeGenericType(elementType);
        return (IQueryable)Activator.CreateInstance(queryableType, this, expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new SanityQueryable<TElement>(this, expression);

    public object? Execute(Expression expression) =>
        ExecuteAsync<object?>(expression, CancellationToken.None).GetAwaiter().GetResult();

    public TResult Execute<TResult>(Expression expression) =>
        ExecuteAsync<TResult>(expression, CancellationToken.None).GetAwaiter().GetResult();

    /// <summary>
    /// Parses the full expression tree into a <see cref="SanityQueryTranslation"/>
    /// and actually executes it against the <see cref="ISanityClient"/>.
    /// <typeparamref name="TResult"/> must be the exact runtime shape the
    /// caller expects (<c>List&lt;TElement&gt;</c> for list-shaped
    /// results, <c>TElement</c>/<c>TElement?</c> for single-item results,
    /// <c>int</c>/<c>long</c>/<c>bool</c> for Count/Any) -- the public
    /// extension methods in <see cref="SanityQueryableExtensions"/> are
    /// what guarantee that.
    /// </summary>
    public async Task<TResult> ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var translation = SanityExpressionWalker.Parse(expression);
        var projectedType = translation.Projector?.ReturnType ?? translation.ElementType;

        switch (translation.Mode)
        {
            case SanityQueryMode.Count:
                {
                    var count = await _client.FetchResultAsync<long>(
                        translation.BuildCountQuery(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return (TResult)(object)(int)count;
                }

            case SanityQueryMode.LongCount:
                {
                    var count = await _client.FetchResultAsync<long>(
                        translation.BuildCountQuery(), cancellationToken: cancellationToken).ConfigureAwait(false);
                    return (TResult)(object)count;
                }

            case SanityQueryMode.Any:
                {
                    var any = await _client.FetchResultAsync<bool>(
                        $"{translation.BuildCountQuery()} > 0", cancellationToken: cancellationToken).ConfigureAwait(false);
                    return (TResult)(object)any;
                }

            case SanityQueryMode.List:
                {
                    var raw = await FetchRawListAsync(translation.ElementType, translation.BuildGroqQuery(), cancellationToken)
                        .ConfigureAwait(false);
                    var projected = translation.Projector is null
                        ? raw
                        : BuildTypedList(projectedType, Project(raw, translation.Projector));
                    return (TResult)projected;
                }

            default:
                {
                    var isSingleFamily = translation.Mode is SanityQueryMode.Single or SanityQueryMode.SingleOrDefault;
                    var limit = isSingleFamily ? 2 : 1;
                    var raw = await FetchRawListAsync(
                        translation.ElementType, translation.BuildGroqQuery(takeOverride: limit), cancellationToken)
                        .ConfigureAwait(false);

                    object? item = PickSingleOrFirst(raw, translation.Mode);
                    if (translation.Projector is not null && item is not null)
                    {
                        item = translation.Projector.Compile().DynamicInvoke(item);
                    }

                    item ??= projectedType.IsValueType ? Activator.CreateInstance(projectedType) : null;
                    return (TResult)item!;
                }
        }
    }

    private static object? PickSingleOrFirst(IList raw, SanityQueryMode mode)
    {
        switch (mode)
        {
            case SanityQueryMode.Single or SanityQueryMode.SingleOrDefault:
                if (raw.Count > 1)
                {
                    throw new InvalidOperationException("Sequence contains more than one element.");
                }

                if (raw.Count == 0)
                {
                    if (mode == SanityQueryMode.Single)
                    {
                        throw new InvalidOperationException("Sequence contains no elements.");
                    }

                    return null;
                }

                return raw[0];

            default: // First / FirstOrDefault
                if (raw.Count == 0)
                {
                    if (mode == SanityQueryMode.First)
                    {
                        throw new InvalidOperationException("Sequence contains no elements.");
                    }

                    return null;
                }

                return raw[0];
        }
    }

    private static IEnumerable<object?> Project(IEnumerable raw, LambdaExpression projector)
    {
        var compiled = projector.Compile();
        foreach (var item in raw)
        {
            yield return compiled.DynamicInvoke(item);
        }
    }

    private static IList BuildTypedList(Type elementType, IEnumerable<object?> items)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var item in items)
        {
            list.Add(item);
        }

        return list;
    }

    /// <summary>
    /// Calls the generic <see cref="ISanityClient.FetchResultAsync{T}"/>
    /// via reflection, since <paramref name="elementType"/> is only known
    /// at runtime here (it comes from the expression tree, not a C#
    /// generic parameter) -- there's no other way to invoke a generic
    /// method with a Type value instead of a compile-time type argument.
    /// </summary>
    private async Task<IList> FetchRawListAsync(Type elementType, string groq, CancellationToken cancellationToken)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var method = FetchResultAsyncMethod.MakeGenericMethod(listType);
        var task = (Task)method.Invoke(_client, [groq, null, null, cancellationToken])!;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty(nameof(Task<object>.Result))!.GetValue(task);
        return (IList?)result ?? (IList)Activator.CreateInstance(listType)!;
    }

    private static Type GetSequenceElementType(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IQueryable<>))
        {
            return type.GetGenericArguments()[0];
        }

        var enumerableInterface = type.GetInterfaces()
            .Prepend(type)
            .FirstOrDefault(t => t.IsGenericType && t.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        return enumerableInterface?.GetGenericArguments()[0]
            ?? throw new NotSupportedException($"Could not determine the element type of '{type}'.");
    }
}
