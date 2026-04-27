namespace Chatter.Rest.UriTemplates;

public sealed class UriTemplate
{
    private readonly IReadOnlyList<object> _tokens;

    public UriTemplate(string template)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        _tokens = UriTemplateParser.Parse(template);
    }

    public string Expand(IDictionary<string, string> variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        // RFC 6570 §2.3: variable names are case-sensitive.  Copy into an
        // ordinal dictionary so lookup is always case-sensitive regardless of
        // the comparer the caller's dictionary was created with.
        // Wrap string values as object? to match the internal expander signature.
        var ordinal = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kvp in variables)
        {
            ordinal[kvp.Key] = kvp.Value;
        }

        var sb = new System.Text.StringBuilder();

        foreach (var token in _tokens)
        {
            if (token is string literal)
            {
                sb.Append(literal);
            }
            else if (token is UriTemplateExpression expression)
            {
                sb.Append(UriTemplateExpander.Expand(expression, ordinal));
            }
        }

        return sb.ToString();
    }

    public string Expand(params (string Key, string Value)[] variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        var dict = new Dictionary<string, string>();

        foreach (var (key, value) in variables)
        {
            // First-wins for duplicates
            if (!dict.ContainsKey(key))
            {
                dict[key] = value;
            }
        }

        return Expand(dict);
    }

    public IReadOnlyList<string> GetVariables()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var token in _tokens)
        {
            if (token is UriTemplateExpression expression)
            {
                foreach (var varSpec in expression.Variables)
                {
                    if (seen.Add(varSpec.Name))
                    {
                        result.Add(varSpec.Name);
                    }
                }
            }
        }

        return result;
    }
}
