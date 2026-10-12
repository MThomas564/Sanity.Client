using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Sanity.Client.Linq;

/// <summary>
/// Translates a subset of LINQ expression trees into GROQ filter
/// fragments. This is a clean-room implementation written against
/// GROQ's own published syntax (sanity.io/docs/groq), not a port of any
/// other library's expression-walking code.
///
/// Supported: ==, !=, &gt;, &gt;=, &lt;, &lt;=, &amp;&amp;, ||, unary !,
/// null checks, nested member access (dot paths), string
/// Contains/StartsWith/EndsWith (via GROQ <c>match</c>), and
/// Enumerable.Contains against a constant/closed-over collection (GROQ
/// <c>in</c>). Anything else throws <see cref="NotSupportedException"/>
/// with the offending expression in the message, rather than silently
/// producing a wrong query.
/// </summary>
internal static class GroqExpressionTranslator
{
    public static string TranslateBoolean(Expression body)
    {
        switch (body)
        {
            case BinaryExpression { NodeType: ExpressionType.AndAlso } b:
                return $"({TranslateBoolean(b.Left)} && {TranslateBoolean(b.Right)})";

            case BinaryExpression { NodeType: ExpressionType.OrElse } b:
                return $"({TranslateBoolean(b.Left)} || {TranslateBoolean(b.Right)})";

            case BinaryExpression b when IsComparison(b.NodeType):
                return TranslateComparison(b);

            case UnaryExpression { NodeType: ExpressionType.Not } u:
                return $"!({TranslateBoolean(u.Operand)})";

            case UnaryExpression { NodeType: ExpressionType.Convert } u:
                // Boxing a bool member (e.g. a bool? comparison's operand
                // wrapped for the Equal node) -- the conversion itself
                // carries no GROQ meaning, just translate the inner value.
                return TranslateBoolean(u.Operand);

            case MethodCallExpression m when IsStringMatch(m, out var fragment):
                return fragment;

            case MethodCallExpression m when IsContains(m, out var fragment):
                return fragment;

            case MemberExpression member when member.Type == typeof(bool):
                // `p => p.IsPublished` with no explicit `== true`.
                return TranslateMemberPath(member);

            default:
                throw NotSupported(body);
        }
    }

    public static string TranslateMemberPath(Expression expression)
    {
        // Strip boxing conversions (e.g. a Nullable<T> member compared to
        // a non-nullable constant) before walking the member chain.
        while (expression is UnaryExpression { NodeType: ExpressionType.Convert } u)
        {
            expression = u.Operand;
        }

        var segments = new List<string>();
        var current = expression;

        while (current is MemberExpression member)
        {
            if (member.Member is not PropertyInfo || IsScalarClrType(member.Member.DeclaringType))
            {
                // Rejects things like `p.Title.Length` or
                // `p.PublishedAt.Year` -- .Length/.Year are real CLR
                // properties (so they pass the PropertyInfo check) but
                // have no GROQ equivalent: a GROQ string/datetime field
                // doesn't have sub-fields the way a nested document does.
                throw NotSupported(expression);
            }

            segments.Insert(0, GroqNameResolver.ResolveFieldName(member.Member));
            current = member.Expression;
        }

        if (current is not ParameterExpression || segments.Count == 0)
        {
            throw NotSupported(expression);
        }

        return string.Join(".", segments);
    }

    /// <summary>
    /// BCL "scalar" types that are never themselves nested Sanity
    /// documents/objects -- a property access rooted past one of these
    /// (e.g. <c>string.Length</c>, <c>DateTime.Year</c>) is a CLR-only
    /// computation with no GROQ field behind it.
    /// </summary>
    private static bool IsScalarClrType(Type? type) => type is not null && (
        type == typeof(string) ||
        type == typeof(DateTime) ||
        type == typeof(DateTimeOffset) ||
        type == typeof(TimeSpan) ||
        type == typeof(Guid) ||
        type.IsPrimitive ||
        type.IsEnum ||
        (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsScalarClrType(Nullable.GetUnderlyingType(type))));

    public static string TranslateConstant(object? value) => value switch
    {
        null => "null",
        bool b => b ? "true" : "false",
        string s => Quote(s),
        DateTimeOffset dto => Quote(dto.ToString("o", CultureInfo.InvariantCulture)),
        DateTime dt => Quote(dt.ToString("o", CultureInfo.InvariantCulture)),
        Enum e => Quote(e.ToString()),
        sbyte or byte or short or ushort or int or uint or long or ulong =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        float or double or decimal =>
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
        Guid g => Quote(g.ToString()),
        _ => throw new NotSupportedException(
            $"Cannot translate a constant of type '{value.GetType()}' into a GROQ literal. " +
            "Supported: string, bool, numeric types, DateTime/DateTimeOffset, Guid, enum, null."),
    };

    private static bool IsComparison(ExpressionType type) => type is
        ExpressionType.Equal or ExpressionType.NotEqual or
        ExpressionType.GreaterThan or ExpressionType.GreaterThanOrEqual or
        ExpressionType.LessThan or ExpressionType.LessThanOrEqual;

    private static string TranslateComparison(BinaryExpression b)
    {
        var op = b.NodeType switch
        {
            ExpressionType.Equal => "==",
            ExpressionType.NotEqual => "!=",
            ExpressionType.GreaterThan => ">",
            ExpressionType.GreaterThanOrEqual => ">=",
            ExpressionType.LessThan => "<",
            ExpressionType.LessThanOrEqual => "<=",
            _ => throw NotSupported(b),
        };

        var (memberSide, valueSide, swapped) = ClassifySides(b.Left, b.Right);
        if (swapped)
        {
            // "5 < p.Field" must become "field > 5", not "field < 5" --
            // flip the operator to match the side swap, not just the text.
            op = op switch
            {
                "<" => ">",
                "<=" => ">=",
                ">" => "<",
                ">=" => "<=",
                _ => op, // == and != are symmetric
            };
        }

        var fieldPath = TranslateMemberPath(memberSide);
        var value = EvaluateConstantSubtree(valueSide);
        return $"{fieldPath} {op} {TranslateConstant(value)}";
    }

    /// <summary>
    /// Decides which side of a binary comparison is the document member
    /// path and which is the (possibly closure-captured) value, so
    /// <c>p => p.Title == title</c> and <c>p => title == p.Title</c> both
    /// work. Also reports whether the sides were swapped relative to the
    /// original left/right, so the caller can flip an asymmetric
    /// operator (&lt;, &gt;, etc.) to match.
    /// </summary>
    private static (Expression Member, Expression Value, bool Swapped) ClassifySides(Expression left, Expression right)
    {
        if (ReferencesParameter(left))
        {
            return (left, right, false);
        }

        if (ReferencesParameter(right))
        {
            return (right, left, true);
        }

        throw new NotSupportedException(
            "A comparison must reference the query's document parameter on at least one side " +
            $"(e.g. 'p.Title == someValue'). Got: left='{left}', right='{right}'.");
    }

    private static bool ReferencesParameter(Expression expression)
    {
        var finder = new ParameterReferenceFinder();
        finder.Visit(expression);
        return finder.Found;
    }

    /// <summary>
    /// Evaluates any subtree that does NOT reference the query's
    /// document parameter -- this is how closure-captured locals
    /// (<c>var minDate = ...; p => p.PublishedAt > minDate</c>) and
    /// arbitrary constant-ish expressions (<c>DateTime.Today</c>,
    /// method calls on captured values, etc.) get their real runtime
    /// value, without needing to special-case every possible C#
    /// compiler representation of "a closed-over variable".
    /// </summary>
    private static object? EvaluateConstantSubtree(Expression expression) => Evaluate(expression);

    /// <summary>
    /// Evaluates any expression subtree that doesn't reference a query
    /// parameter -- closure-captured locals, <c>DateTime.Today</c>,
    /// <c>Skip</c>/<c>Take</c> count arguments, etc. -- to its real
    /// runtime value. Exposed internally so <see cref="SanityExpressionWalker"/>
    /// can reuse it for non-predicate arguments (Skip/Take counts,
    /// Select's lambda itself is NOT evaluated this way -- only plain
    /// value-producing subtrees are).
    /// </summary>
    internal static object? Evaluate(Expression expression)
    {
        if (expression is ConstantExpression constant)
        {
            return constant.Value;
        }

        try
        {
            return Expression.Lambda(expression).Compile().DynamicInvoke();
        }
        catch (Exception ex)
        {
            throw new NotSupportedException(
                $"Could not evaluate '{expression}' as a constant value.", ex);
        }
    }

    private static bool IsStringMatch(MethodCallExpression m, out string fragment)
    {
        fragment = string.Empty;

        if (m.Method.DeclaringType != typeof(string) || m.Object is null || m.Arguments.Count != 1)
        {
            return false;
        }

        var pattern = m.Method.Name switch
        {
            nameof(string.Contains) => "*{0}*",
            nameof(string.StartsWith) => "{0}*",
            nameof(string.EndsWith) => "*{0}",
            _ => null,
        };

        if (pattern is null)
        {
            return false;
        }

        var fieldPath = TranslateMemberPath(m.Object);
        var value = EvaluateConstantSubtree(m.Arguments[0]) as string
            ?? throw new NotSupportedException($"'{m}' must be called with a string argument.");
        fragment = $"{fieldPath} match {Quote(string.Format(CultureInfo.InvariantCulture, pattern, EscapeMatchValue(value)))}";
        return true;
    }

    private static bool IsContains(MethodCallExpression m, out string fragment)
    {
        fragment = string.Empty;

        Expression source;
        Expression value;

        if (m.Method.Name == nameof(Enumerable.Contains) &&
            (m.Method.DeclaringType == typeof(Enumerable) || m.Method.DeclaringType == typeof(MemoryExtensions)) &&
            m.Arguments.Count == 2)
        {
            // Static extension-style call: Enumerable.Contains(source,
            // value) for most collections, or (since recent .NET
            // compilers prefer the allocation-free overload for arrays)
            // MemoryExtensions.Contains(ReadOnlySpan<T>, T) -- the
            // array-to-span conversion shows up as an extra Convert node
            // around the source argument, stripped below.
            (source, value) = (m.Arguments[0], m.Arguments[1]);
        }
        else if (m.Method.Name == nameof(Enumerable.Contains) && m.Object is not null && m.Arguments.Count == 1)
        {
            // Instance-style call: list.Contains(x) for List<T>,
            // HashSet<T>, etc.
            (source, value) = (m.Object, m.Arguments[0]);
        }
        else
        {
            return false;
        }

        while (true)
        {
            if (source is UnaryExpression { NodeType: ExpressionType.Convert } u)
            {
                source = u.Operand;
                continue;
            }

            // Array-to-span conversions (chosen by modern compilers for
            // the allocation-free MemoryExtensions.Contains overload)
            // show up as a static op_Implicit/AsSpan call wrapping the
            // real closure access, not a Convert node.
            if (source is MethodCallExpression { Arguments.Count: 1 } span &&
                span.Method.IsStatic &&
                (span.Method.Name == "op_Implicit" || span.Method.Name == "AsSpan"))
            {
                source = span.Arguments[0];
                continue;
            }

            break;
        }

        if (!ReferencesParameter(value) && ReferencesParameter(source))
        {
            // e.g. list.Contains(p.Field) where list, unusually, is
            // written as the non-extension-style first argument.
            (source, value) = (value, source);
        }

        if (ReferencesParameter(source) || !ReferencesParameter(value))
        {
            return false;
        }

        var fieldPath = TranslateMemberPath(value);
        var collection = EvaluateConstantSubtree(source) as System.Collections.IEnumerable
            ?? throw new NotSupportedException($"'{m}' must be called against a constant/closed-over collection.");

        var literals = collection.Cast<object?>().Select(TranslateConstant);
        fragment = $"{fieldPath} in [{string.Join(", ", literals)}]";
        return true;
    }

    private static string Quote(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    // GROQ's `match` operator treats *, ?, and other glob-ish characters
    // specially; escaping them keeps a literal Contains/StartsWith/EndsWith
    // call from accidentally becoming a wildcard pattern.
    private static string EscapeMatchValue(string value) => value.Replace("*", "\\*").Replace("?", "\\?");

    private static NotSupportedException NotSupported(Expression expression) => new(
        $"This LINQ expression is not supported by the GROQ translator: '{expression}'. " +
        "See the 'Supported LINQ operators' section of the Sanity.Client README for what's translated.");

    private sealed class ParameterReferenceFinder : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (Found)
            {
                return node;
            }

            return base.Visit(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found = true;
            return node;
        }
    }
}
