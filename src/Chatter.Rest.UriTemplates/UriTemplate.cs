namespace Chatter.Rest.UriTemplates;

public sealed class UriTemplate
{
    private readonly IReadOnlyList<UriTemplateToken> _tokens;
    private readonly IUriTemplateExpander _expander;
    private readonly HashSet<string> _prefixModifiedVariables;

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
        _prefixModifiedVariables = CollectPrefixModifiedVariables(tokens);
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
    ///   <item><description><see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> with <see cref="string"/> key and <see cref="string"/> value — associative array value. Supplied order is preserved verbatim, duplicates included, except for <see cref="ISet{T}"/> implementations, which are unordered and are canonicalized by ordinal key order; see <c>docs/usage.md</c> for the full ordering contract. An empty sequence is treated as undefined.</description></item>
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
            ordinal[kvp.Key] = kvp.Value;
        }

        // Values are copied across as-is above; nothing is read from them until
        // PrepareVariables has walked the template in order.
        PrepareVariables(ordinal);

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
                dict[key] = value;
            }
        }

        // The tuple array's order is the caller's, not the template's; nothing is read
        // from a value until PrepareVariables has walked the template in order.
        PrepareVariables(dict);

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
    /// Brings the caller's values into the state expansion requires, walking the
    /// template's tokens once, in order, and completing all of one variable's work
    /// before looking at the next.
    /// <para>
    /// The rule the walk follows: every failure is reported at the point in the
    /// template where it becomes unavoidable, and no value is read on the way to a
    /// failure that is already certain.
    /// </para>
    /// <para>
    /// At a variable's first reference its value's type is checked against the
    /// supported set, and the value is then snapshotted — with each member validated
    /// as it is copied. A composite bound to a name the template prefixes anywhere is
    /// the exception: it is marked pending instead, which skips the snapshot without
    /// raising anything, because its failure does not become unavoidable until the
    /// walk reaches the reference that carries the prefix. Every failure
    /// <see cref="UriTemplateExpander"/> can raise for a value is raised here rather
    /// than during expansion, with the expander's message verbatim.
    /// </para>
    /// <para>
    /// Marking a variable pending cannot lose its failure. A name is pending only
    /// because some varspec in this template carries a prefix over it, and the walk
    /// visits every varspec of every expression in order, so it must arrive at that
    /// varspec — where the prefix violation is raised — unless something earlier in
    /// the template throws first, which is the outcome template order calls for
    /// anyway. Deferring the throw is therefore a change of position, never of
    /// whether the input is rejected.
    /// </para>
    /// <para>
    /// The alternative — validating everything, then snapshotting everything, then
    /// expanding — is what this replaces. Each of those phases was internally ordered
    /// by the template, but a later variable's work still ran before an earlier
    /// variable's failure surfaced. Ordering is now a property of the single walk
    /// rather than of three independent ones.
    /// </para>
    /// <para>
    /// Values the template never references are never visited, so an unused lazy,
    /// blocking, or infinite sequence is left alone; a value referenced from several
    /// expressions is prepared exactly once, and a pending one not at all.
    /// </para>
    /// </summary>
    private void PrepareVariables(Dictionary<string, object?> variables)
    {
        var prepared = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string>? pendingPrefixViolations = null;

        foreach (var token in _tokens)
        {
            if (token is not UriTemplateExpressionToken expression)
            {
                continue;
            }

            foreach (var varSpec in expression.Variables)
            {
                var name = varSpec.Name;

                // The first reference is where the variable's own work happens; later
                // references add nothing beyond the prefix modifier they may carry,
                // which is handled below. Guarding on `prepared` is also what keeps a
                // value referenced three times from being snapshotted more than once.
                if (prepared.Add(name)
                    && variables.TryGetValue(name, out var value)
                    && value is not null
                    && value is not string)
                {
                    // Undefined values and simple strings need neither validation nor a
                    // snapshot, and a prefix modifier over a string is legal.
                    //
                    // Order matters, and mirrors the expander: an unsupported type is
                    // reported as such, here at the variable's own position, rather than
                    // as a misapplied prefix modifier. That is why "{bad}{bad:3}" over an
                    // unsupported value reports the type and not the prefix.
                    ThrowIfUnsupportedType(name, value);

                    if (_prefixModifiedVariables.Contains(name))
                    {
                        // A composite the template prefixes somewhere cannot expand, so
                        // materializing it would read a value on the way to a certain
                        // failure. Skip the snapshot, but leave the failure to the
                        // reference that actually carries the prefix, so that variables
                        // between here and there keep their precedence.
                        pendingPrefixViolations ??= new HashSet<string>(StringComparer.Ordinal);
                        pendingPrefixViolations.Add(name);
                    }
                    else
                    {
                        variables[name] = Snapshot(name, value);
                    }
                }

                // Reached in template order, including on the very iteration that marked
                // the variable pending — a prefix on the first reference fails there.
                if (varSpec.PrefixLength.HasValue
                    && pendingPrefixViolations is not null
                    && pendingPrefixViolations.Contains(name))
                {
                    throw new FormatException(
                        $"Prefix modifier is not applicable to composite values per RFC 6570 (variable '{name}').");
                }
            }
        }
    }

    /// <summary>
    /// Reports a value whose runtime type is none of the shapes RFC 6570 expansion
    /// supports, with the message <see cref="UriTemplateExpander"/> would have produced
    /// on reaching the variable. Only a type test is performed, so nothing is read from
    /// the value.
    /// <para>
    /// Hoisting this check out of the expander is what makes an earlier variable's
    /// unsupported value win over a later variable's enumeration: the expander could
    /// only report it after every snapshot had already been taken.
    /// </para>
    /// </summary>
    private static void ThrowIfUnsupportedType(string name, object value)
    {
        if (value is IDictionary<string, string>
            || value is IEnumerable<KeyValuePair<string, string>>
            || value is IEnumerable<string>)
        {
            return;
        }

        throw new FormatException(
            $"Variable '{name}' has unsupported type '{value.GetType().FullName}'. " +
            "Expected string, IEnumerable<string>, IDictionary<string, string>, or IEnumerable<KeyValuePair<string, string>>.");
    }

    /// <summary>
    /// Collects the names the template references with a prefix modifier, so
    /// the prefix check inside <see cref="PrepareVariables(Dictionary{string, object})"/>
    /// costs a set lookup rather than a second walk per variable.
    /// </summary>
    private static HashSet<string> CollectPrefixModifiedVariables(IReadOnlyList<UriTemplateToken> tokens)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var token in tokens)
        {
            if (token is not UriTemplateExpressionToken expression)
            {
                continue;
            }

            foreach (var varSpec in expression.Variables)
            {
                if (varSpec.PrefixLength.HasValue)
                {
                    names.Add(varSpec.Name);
                }
            }
        }

        return names;
    }

    /// <summary>
    /// Takes an owned snapshot of a caller-supplied composite value, validating each
    /// member as it is copied.
    /// <para>
    /// A template may reference the same variable from more than one expression, and each
    /// expression materializes the value independently. Without a snapshot, a single-pass or
    /// lazily evaluated sequence would be drained by the first expression, and a mutable
    /// collection could change (or throw) between expressions. Copying once here guarantees that
    /// every expression observes the same values and that the caller's sequence is enumerated
    /// exactly once.
    /// </para>
    /// <para>
    /// Members are validated during the copy rather than after it. Copying first and
    /// leaving validation to the expander would keep reading a sequence past its first
    /// invalid member, so a null key, null value, or null element followed by a throwing,
    /// blocking, or endless remainder would never be reported. Validating in step makes
    /// the first invalid member the observable failure and stops the enumeration there.
    /// Every message below is the one <see cref="UriTemplateExpander"/> produces for the
    /// same input, naming the same variable.
    /// </para>
    /// <para>
    /// The type dispatch mirrors <see cref="UriTemplateExpander"/>, which selects its
    /// associative-array ordering by runtime type: each branch copies into a type that
    /// still satisfies the interface the expander tests for, so no snapshot can change
    /// which branch a value takes. Unsupported types never reach this method.
    /// </para>
    /// </summary>
    private static object? Snapshot(string name, object value)
    {
        // Every branch below is selected by a runtime type test, which reads nothing from
        // the value. Type and prefix validation have already run for this variable.

        if (value is IDictionary<string, string> dictionaryValue)
        {
            // Copy into a dictionary, not a list: UriTemplateExpander dispatches on the
            // runtime type, and its ordinal key ordering is selected by that type rather
            // than by the contents of the sequence. Flattening to IEnumerable<KeyValuePair<,>>
            // here would silently opt these values out of that ordering.
            //
            // The entries are copied by hand rather than through the copy constructor:
            // Dictionary<,> rejects a null key with ArgumentNullException, which would
            // replace this overload's documented FormatException with a different type.
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var entry in dictionaryValue)
            {
                ThrowIfNullPair(name, entry);

                snapshot[entry.Key] = entry.Value;
            }

            return snapshot;
        }

        if (value is ISet<KeyValuePair<string, string>> setValue)
        {
            // A set has no meaningful order of its own, and the expander canonicalizes
            // one by ordinal key order — but only for a value that still presents as an
            // ISet<KeyValuePair<string, string>>. Copying into a List here, as the pairs
            // branch below does, would hand the expander an ordered sequence and make the
            // result depend on the caller's set implementation. The HashSet's own
            // enumeration order does not matter for the same reason: it is canonicalized
            // downstream, not here.
            var snapshot = new HashSet<KeyValuePair<string, string>>();

            foreach (var entry in setValue)
            {
                ThrowIfNullPair(name, entry);

                snapshot.Add(entry);
            }

            return snapshot;
        }

        if (value is IEnumerable<KeyValuePair<string, string>> pairsValue)
        {
            var snapshot = new List<KeyValuePair<string, string>>();

            foreach (var entry in pairsValue)
            {
                ThrowIfNullPair(name, entry);

                snapshot.Add(entry);
            }

            return snapshot;
        }

        var listValue = (IEnumerable<string>)value;
        var items = new List<string>();

        foreach (var item in listValue)
        {
            if (item is null)
            {
                throw new FormatException(
                    $"Variable '{name}' contains a null element. List elements must be non-null strings.");
            }

            items.Add(item);
        }

        return items;
    }

    /// <summary>
    /// Rejects a null key or a null value in an associative-array member, with the
    /// message and the key/value precedence <c>UriTemplateExpander.ExpandAssociativeArray</c>
    /// uses for the same member.
    /// </summary>
    private static void ThrowIfNullPair(string name, KeyValuePair<string, string> entry)
    {
        if (entry.Key is null)
        {
            throw new FormatException(
                $"Variable '{name}' contains a null key. " +
                "Associative array keys must be non-null strings.");
        }

        if (entry.Value is null)
        {
            throw new FormatException(
                $"Variable '{name}' contains a null value for key '{entry.Key}'. " +
                "Associative array values must be non-null strings.");
        }
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
