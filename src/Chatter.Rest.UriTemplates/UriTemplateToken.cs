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

    public UriTemplateExpressionToken(UriTemplateOperator @operator, IReadOnlyList<UriTemplateVarSpec> variables)
    {
        Operator = @operator;
        Variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }
}
