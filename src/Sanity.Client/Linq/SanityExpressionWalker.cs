using System.Linq.Expressions;

namespace Sanity.Client.Linq;

/// <summary>
/// Marker implemented by the root <see cref="SanityQueryable{T}"/> a
/// chain of <c>Queryable</c> extension method calls (Where/Select/
/// OrderBy/Skip/Take/...) ultimately bottoms out at, so
/// <see cref="SanityExpressionWalker"/> can recognize where the
/// expression tree ends and recover the element type / GROQ type name.
/// </summary>
internal interface ISanityQueryRoot
{
    Type ElementType { get; }
}

/// <summary>
/// Walks the expression tree produced by chaining standard
/// <see cref="System.Linq.Queryable"/> extension methods against a
/// <see cref="SanityQueryable{T}"/>, and turns it into a
/// <see cref="SanityQueryTranslation"/>.
///
/// The tree is nested outside-in (the last method called is the
/// outermost node, wrapping everything before it as its first
/// argument), so this walks down to the root first, then applies each
/// operation in the order it was actually written.
/// </summary>
internal static class SanityExpressionWalker
{
    public static SanityQueryTranslation Parse(Expression expression)
    {
        var nodes = new List<MethodCallExpression>();
        var current = expression;

        while (current is MethodCallExpression call)
        {
            nodes.Add(call);
            current = call.Arguments[0];
        }

        if (current is not ConstantExpression { Value: ISanityQueryRoot root })
        {
            throw new NotSupportedException(
                $"Could not find the root Sanity query in this expression tree ('{expression}'). " +
                "Only expressions built entirely from a Sanity Query<T>() source are supported.");
        }

        var translation = new SanityQueryTranslation
        {
            ElementType = root.ElementType,
            TypeName = GroqNameResolver.ResolveTypeName(root.ElementType),
        };

        // nodes[] was collected outermost-first; apply root-to-outermost,
        // i.e. in the order the user actually chained them.
        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            Apply(translation, nodes[i]);
        }

        return translation;
    }

    private static void Apply(SanityQueryTranslation translation, MethodCallExpression call)
    {
        switch (call.Method.Name)
        {
            case "Where":
                translation.WhereFragments.Add(GroqExpressionTranslator.TranslateBoolean(GetLambda(call.Arguments[1]).Body));
                break;

            case "OrderBy":
                translation.OrderClauses.Clear();
                translation.OrderClauses.Add(new SanityOrderClause(GetOrderField(call), Descending: false));
                break;

            case "OrderByDescending":
                translation.OrderClauses.Clear();
                translation.OrderClauses.Add(new SanityOrderClause(GetOrderField(call), Descending: true));
                break;

            case "ThenBy":
                translation.OrderClauses.Add(new SanityOrderClause(GetOrderField(call), Descending: false));
                break;

            case "ThenByDescending":
                translation.OrderClauses.Add(new SanityOrderClause(GetOrderField(call), Descending: true));
                break;

            case "Skip":
                translation.SkipCount = (int)GroqExpressionTranslator.Evaluate(call.Arguments[1])!;
                break;

            case "Take":
                translation.TakeCount = (int)GroqExpressionTranslator.Evaluate(call.Arguments[1])!;
                break;

            case "Select":
                translation.Projector = GetLambda(call.Arguments[1]);
                break;

            case "Count" or "LongCount":
                ApplyOptionalPredicate(translation, call);
                translation.Mode = call.Method.Name == "Count" ? SanityQueryMode.Count : SanityQueryMode.LongCount;
                break;

            case "First":
                ApplyOptionalPredicate(translation, call);
                translation.Mode = SanityQueryMode.First;
                break;

            case "FirstOrDefault":
                ApplyOptionalPredicate(translation, call);
                translation.Mode = SanityQueryMode.FirstOrDefault;
                break;

            case "Single":
                ApplyOptionalPredicate(translation, call);
                translation.Mode = SanityQueryMode.Single;
                break;

            case "SingleOrDefault":
                ApplyOptionalPredicate(translation, call);
                translation.Mode = SanityQueryMode.SingleOrDefault;
                break;

            case "Any":
                ApplyOptionalPredicate(translation, call);
                translation.Mode = SanityQueryMode.Any;
                break;

            default:
                throw new NotSupportedException(
                    $"The LINQ operator '{call.Method.Name}' is not supported against a Sanity query. " +
                    "See the 'Supported LINQ operators' section of the Sanity.Client README.");
        }
    }

    private static void ApplyOptionalPredicate(SanityQueryTranslation translation, MethodCallExpression call)
    {
        if (call.Arguments.Count == 2)
        {
            translation.WhereFragments.Add(GroqExpressionTranslator.TranslateBoolean(GetLambda(call.Arguments[1]).Body));
        }
    }

    private static string GetOrderField(MethodCallExpression call) =>
        GroqExpressionTranslator.TranslateMemberPath(GetLambda(call.Arguments[1]).Body);

    private static LambdaExpression GetLambda(Expression argument) => argument switch
    {
        UnaryExpression { NodeType: ExpressionType.Quote } quote => (LambdaExpression)quote.Operand,
        LambdaExpression lambda => lambda,
        _ => throw new NotSupportedException($"Expected a lambda expression argument, got '{argument}'."),
    };
}
