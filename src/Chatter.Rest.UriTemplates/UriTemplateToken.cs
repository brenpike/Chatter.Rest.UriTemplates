namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Base type for parsed URI template tokens.
/// </summary>
/// <remarks>
/// A parsed template is a sequence of tokens alternating between
/// <see cref="UriTemplateLiteralToken"/> and <see cref="UriTemplateExpressionToken"/> segments.
/// The private protected constructor prevents subclassing outside this assembly.
/// </remarks>
public abstract class UriTemplateToken
{
    private protected UriTemplateToken() { }
}

/// <summary>
/// A literal text segment of a URI template.
/// </summary>
public sealed class UriTemplateLiteralToken : UriTemplateToken
{
    /// <summary>
    /// Gets the literal text of this segment. Never null.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Creates a literal token holding the given text.
    /// </summary>
    /// <param name="value">The literal text of the segment.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
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
    /// <summary>
    /// Gets the operator of this expression.
    /// </summary>
    public UriTemplateOperator Operator { get; }

    /// <summary>
    /// Gets the variable specifications of this expression. The list is a read-only,
    /// defensively copied snapshot taken at construction: later mutation of the collection
    /// passed to the constructor does not affect it, and casting it to a mutable collection
    /// interface cannot alter the token. It is never empty and contains no null elements.
    /// </summary>
    public IReadOnlyList<UriTemplateVarSpec> Variables { get; }

    /// <summary>
    /// Creates an expression token. The supplied variable specifications are copied into a
    /// read-only wrapper, so neither later mutation of <paramref name="variables"/> nor casting
    /// <see cref="Variables"/> to a mutable collection interface can alter the token.
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

        // Defensive copy: the token must not alias a caller-owned collection.
        var copy = new UriTemplateVarSpec[variables.Count];

        for (var i = 0; i < variables.Count; i++)
        {
            copy[i] = variables[i] ?? throw new ArgumentException(
                $"Variable specification at index {i} is null.", nameof(variables));
        }

        Operator = @operator;

        // Wrap the copy: a bare array is an IList<T>, so exposing it would let a caller cast
        // Variables back to IList<UriTemplateVarSpec> (or UriTemplateVarSpec[]) and replace
        // entries, including with null. ReadOnlyCollection<T> throws from every IList<T>
        // mutator, and is available on both net8.0 and netstandard2.0.
        Variables = Array.AsReadOnly(copy);
    }
}
