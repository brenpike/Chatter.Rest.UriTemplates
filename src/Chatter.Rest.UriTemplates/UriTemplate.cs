namespace Chatter.Rest.UriTemplates;

public sealed class UriTemplate
{
    private readonly IReadOnlyList<UriTemplateToken> _tokens;
    private readonly IUriTemplateExpander _expander;

    public UriTemplate(string template)
        : this(template, UriTemplateParser.Default, UriTemplateExpander.Default)
    {
    }

    internal UriTemplate(string template, IUriTemplateParser parser, IUriTemplateExpander expander)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        var tokens = parser.Parse(template);

        if (tokens is null)
        {
            throw new InvalidOperationException(
                $"The URI template parser '{parser.GetType().FullName}' returned null from " +
                $"{nameof(IUriTemplateParser)}.{nameof(IUriTemplateParser.Parse)}. " +
                "A parser must return a non-null token list.");
        }

        _tokens = tokens;
        _expander = expander;
    }

    /// <summary>
    /// Expands the URI template using the provided variable dictionary.
    /// All values are treated as simple strings (Levels 1–3).
    /// <para>
    /// A <see langword="null"/> value is treated as undefined per RFC 6570 §2.3 (the variable
    /// is omitted), matching <see cref="Expand(IDictionary{string, object})"/>. The value type is
    /// declared non-nullable, so a null can only arrive from a caller that bypasses nullable
    /// reference type analysis (for example a <c>netstandard2.0</c> consumer); such a caller gets
    /// an omitted variable rather than an exception.
    /// </para>
    /// </summary>
    /// <param name="variables">A dictionary mapping variable names to string values.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    public string Expand(IDictionary<string, string> variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        // Wrap string values as object? and delegate to the canonical overload.
        var wrapped = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kvp in variables)
        {
            wrapped[kvp.Key] = kvp.Value;
        }

        return ExpandCore(wrapped);
    }

    /// <summary>
    /// Expands the URI template using the provided variable dictionary, supporting
    /// composite value types for RFC 6570 Level 4 expansion.
    /// <para>
    /// Supported value types:
    /// <list type="bullet">
    ///   <item><description><see langword="null"/> — treated as undefined per RFC 6570 §2.3 (variable is omitted).</description></item>
    ///   <item><description><see cref="string"/> — simple string value. Supports Level 1–3 expansion and Level 4 prefix (<c>:N</c>) truncation when specified in the template.</description></item>
    ///   <item><description><see cref="IEnumerable{T}"/> of <see cref="string"/> — list value. Expanded per RFC 6570 composite rules; an empty list is treated as undefined.</description></item>
    ///   <item><description><see cref="IDictionary{TKey, TValue}"/> of <see cref="string"/> to <see cref="string"/> — associative array value. An empty dictionary is treated as undefined.</description></item>
    ///   <item><description><see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> with <see cref="string"/> key and <see cref="string"/> value — associative array value (preserves insertion order). An empty sequence is treated as undefined.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="variables">A dictionary mapping variable names to values of the supported types listed above.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when a variable value is not one of the supported types, when a prefix modifier
    /// is applied to a composite value, or when a composite value contains null elements.
    /// </exception>
    public string Expand(IDictionary<string, object?> variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        // RFC 6570 §2.3: variable names are case-sensitive.  Copy into an
        // ordinal dictionary so lookup is always case-sensitive regardless of
        // the comparer the caller's dictionary was created with.
        var ordinal = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kvp in variables)
        {
            ordinal[kvp.Key] = Snapshot(kvp.Value);
        }

        return ExpandCore(ordinal);
    }

    /// <summary>
    /// Expands the URI template using the provided dictionary of
    /// <see cref="UriTemplateValue"/> instances, supporting all RFC 6570
    /// Level 1–4 value types through a strongly-typed API.
    /// <para>
    /// A <see langword="null"/> entry is treated as undefined per RFC 6570 §2.3 (the variable is
    /// omitted), matching <see cref="Expand(IDictionary{string, object})"/>.
    /// </para>
    /// </summary>
    /// <param name="variables">A dictionary mapping variable names to <see cref="UriTemplateValue"/> instances.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    public string Expand(IDictionary<string, UriTemplateValue> variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        // Convert UriTemplateValue entries to the canonical object? representation
        // once upfront, then delegate to ExpandCore — O(variables) total instead of
        // O(expressions × variables).
        var mapped = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kvp in variables)
        {
            // A null entry is undefined per RFC 6570 §2.3, consistent with the object?
            // overload.  Mapping it here, rather than letting it reach
            // UriTemplateExpander.MapValue, also keeps this method from surfacing an
            // ArgumentException whose paramName does not exist on it.
            mapped[kvp.Key] = kvp.Value is null
                ? null
                : UriTemplateExpander.MapValue(kvp.Key, kvp.Value);
        }

        return ExpandCore(mapped);
    }

    /// <summary>
    /// Canonical expansion path. Accepts a pre-built ordinal dictionary and
    /// iterates tokens, delegating expression expansion to
    /// <see cref="UriTemplateExpander"/>.
    /// </summary>
    private string ExpandCore(Dictionary<string, object?> ordinalVariables)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var token in _tokens)
        {
            if (token is UriTemplateLiteralToken literal)
            {
                sb.Append(literal.Value);
            }
            else if (token is UriTemplateExpressionToken expression)
            {
                sb.Append(_expander.Expand(expression, ordinalVariables));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Expands the URI template using the provided variable tuples. All values are treated as
    /// simple strings (Levels 1–3).
    /// <para>When duplicate keys are present, the first occurrence wins.</para>
    /// <para>
    /// A <see langword="null"/> value is treated as undefined per RFC 6570 §2.3 (the variable
    /// is omitted), matching <see cref="Expand(IDictionary{string, string})"/>.
    /// </para>
    /// </summary>
    /// <param name="variables">Name/value tuples. Neither the array nor any key may be null.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an entry has a null key; the message names the entry index.</exception>
    public string Expand(params (string Key, string Value)[] variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < variables.Length; i++)
        {
            var (key, value) = variables[i];

            ThrowIfKeyIsNull(key, i, nameof(variables));

            // First-wins for duplicates
            if (!dict.ContainsKey(key))
            {
                dict[key] = value;
            }
        }

        return Expand(dict);
    }

    /// <summary>
    /// Expands the URI template using the provided variable tuples, supporting composite
    /// value types for RFC 6570 Level 4 expansion.
    /// <para>
    /// Supported value types for <paramref name="variables"/> entries:
    /// <list type="bullet">
    ///   <item><description><see langword="null"/> — treated as undefined per RFC 6570 §2.3.</description></item>
    ///   <item><description><see cref="string"/> — simple string value.</description></item>
    ///   <item><description><see cref="IEnumerable{T}"/> of <see cref="string"/> — list value.</description></item>
    ///   <item><description><see cref="IDictionary{TKey,TValue}"/> of <see cref="string"/> to <see cref="string"/> — associative array.</description></item>
    /// </list>
    /// </para>
    /// <para>When duplicate keys are present, the first occurrence wins.</para>
    /// <para>
    /// Note: passing a <see cref="UriTemplateValue"/> instance through this overload will throw
    /// <see cref="FormatException"/>. Use <see cref="Expand(IDictionary{string,UriTemplateValue})"/> instead.
    /// </para>
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an entry has a null key; the message names the entry index.</exception>
    /// <exception cref="FormatException">Thrown when a value is not a supported type.</exception>
    public string Expand(params (string Key, object? Value)[] variables)
    {
        if (variables is null)
        {
            throw new ArgumentNullException(nameof(variables));
        }

        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);

        for (var i = 0; i < variables.Length; i++)
        {
            var (key, value) = variables[i];

            ThrowIfKeyIsNull(key, i, nameof(variables));

            // First-wins for duplicates
            if (!dict.ContainsKey(key))
            {
                dict[key] = Snapshot(value);
            }
        }

        return ExpandCore(dict);
    }

    /// <summary>
    /// Rejects a null variable name with a message that identifies the offending entry,
    /// instead of letting <see cref="Dictionary{TKey, TValue}"/> report a <c>key</c> parameter
    /// that does not exist on the called overload.
    /// </summary>
    private static void ThrowIfKeyIsNull(string key, int index, string paramName)
    {
        if (key is null)
        {
            throw new ArgumentException(
                $"The variable name of the entry at index {index} is null. Variable names must be non-null strings.",
                paramName);
        }
    }

    /// <summary>
    /// Takes an owned snapshot of a caller-supplied composite value.
    /// <para>
    /// A template may reference the same variable from more than one expression, and each
    /// expression materializes the value independently. Without a snapshot, a single-pass or
    /// lazily evaluated sequence would be drained by the first expression, and a mutable
    /// collection could change (or throw) between expressions. Copying once here guarantees that
    /// every expression observes the same values and that the caller's sequence is enumerated
    /// exactly once.
    /// </para>
    /// <para>
    /// The type dispatch mirrors <see cref="UriTemplateExpander"/>: associative arrays are
    /// recognized before lists. Unsupported types are passed through unchanged so the expander
    /// still reports them as a <see cref="FormatException"/>.
    /// </para>
    /// </summary>
    private static object? Snapshot(object? value)
    {
        if (value is null || value is string)
        {
            return value;
        }

        if (value is IDictionary<string, string> dictionaryValue)
        {
            return new List<KeyValuePair<string, string>>(dictionaryValue);
        }

        if (value is IEnumerable<KeyValuePair<string, string>> pairsValue)
        {
            return new List<KeyValuePair<string, string>>(pairsValue);
        }

        if (value is IEnumerable<string> listValue)
        {
            return new List<string>(listValue);
        }

        return value;
    }

    /// <summary>
    /// Expands the URI template with no variables. All variable references are treated as undefined.
    /// </summary>
    /// <returns>The expanded URI string with all variables omitted.</returns>
    public string Expand() => ExpandCore(new Dictionary<string, object?>(StringComparer.Ordinal));

    public IReadOnlyList<string> GetVariables()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();

        foreach (var token in _tokens)
        {
            if (token is UriTemplateExpressionToken expression)
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
