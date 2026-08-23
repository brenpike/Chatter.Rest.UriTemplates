namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Base type for parsed URI template tokens.
/// </summary>
public abstract class UriTemplateToken
{
    private protected UriTemplateToken() { }
}

/// <summary>
/// A literal text segment of a URI template.
/// </summary>
public sealed class UriTemplateLiteralToken : UriTemplateToken
{
    public string Value { get; }

    public UriTemplateLiteralToken(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}

/// <summary>
/// An expression segment of a URI template (e.g., {+var}, {?key}).
/// </summary>
public sealed class UriTemplateExpressionToken : UriTemplateToken
{
    public UriTemplateOperator Operator { get; }
    public IReadOnlyList<UriTemplateVarSpec> Variables { get; }

    /// <summary>
    /// Creates an expression token. The supplied variable specifications are copied,
    /// so later mutation of <paramref name="variables"/> cannot alter the token.
    /// </summary>
    /// <param name="operator">The expression operator.</param>
    /// <param name="variables">One or more non-null variable specifications.</param>
    /// <exception cref="ArgumentNullException"><paramref name="variables"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="variables"/> is empty or contains a null element.
    /// </exception>
    public UriTemplateExpressionToken(UriTemplateOperator @operator, IReadOnlyList<UriTemplateVarSpec> variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        if (variables.Count == 0)
        {
            throw new ArgumentException(
                "An expression must declare at least one variable specification.", nameof(variables));
        }

        // Defensive copy: the token exposes a read-only view that callers must not be able to mutate.
        var copy = new UriTemplateVarSpec[variables.Count];

        for (var i = 0; i < variables.Count; i++)
        {
            copy[i] = variables[i] ?? throw new ArgumentException(
                $"Variable specification at index {i} is null.", nameof(variables));
        }

        Operator = @operator;
        Variables = copy;
    }
}
