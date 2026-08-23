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

        // Copy before anything is derived from the list: the tokens that will be
        // expanded are then fixed at construction, whatever the parser does with its own
        // list afterwards.
        _tokens = CopyTokens(tokens);
        _expander = expander;
    }

    /// <summary>
    /// Takes the template's own copy of the parser's result.
    /// <para>
    /// <see cref="IUriTemplateParser"/> is a public extension point, so the returned list
    /// is caller-owned: it may be a mutable <see cref="List{T}"/> the parser keeps a
    /// reference to, or a read-only facade over one. Retaining it would leave the token
    /// sequence free to change between construction and expansion, so a template could
    /// expand something other than what it was constructed from. A varspec's prefix
    /// modifier is the sharpest case: <see cref="ExpandCore"/> reads it to decide whether
    /// a reference will consume its variable's value, so a prefix appearing after
    /// construction would have a composite enumerated on the way to a violation the
    /// expander reports anyway, and a prefix disappearing would skip the snapshot and
    /// leave a single-pass value to be drained twice.
    /// </para>
    /// <para>
    /// The list is enumerated exactly once, so a hostile implementation cannot report one
    /// sequence here and a different one to any later walk. The copy is wrapped rather
    /// than held as a bare array for the reason <see cref="UriTemplateExpressionToken"/>
    /// wraps its own variable list: an array is an <see cref="IList{T}"/>, so anything
    /// that got hold of it could cast it back and write through it.
    /// </para>
    /// </summary>
    private static System.Collections.ObjectModel.ReadOnlyCollection<UriTemplateToken> CopyTokens(IReadOnlyList<UriTemplateToken> tokens)
    {
        var copy = new List<UriTemplateToken>();

        foreach (var token in tokens)
        {
            copy.Add(token);
        }

        return Array.AsReadOnly(copy.ToArray());
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

        // Values are copied across as-is above; nothing is read from any of them until
        // ExpandCore reaches the expression that names it.
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
    /// Canonical expansion path. Walks the template's tokens once, preparing each
    /// expression's variables immediately before that expression is expanded and
    /// delegating the expansion itself to <see cref="UriTemplateExpander"/>.
    /// <para>
    /// Preparation is interleaved with expansion rather than run as a pass of its own.
    /// One expression is completely expanded before the next expression's values are
    /// looked at, so whichever failure the template reaches first is the one raised —
    /// an unsupported type, a prefix modifier over a composite, a null member, a string
    /// that cannot be encoded, an operator outside the defined set — without this method
    /// needing to know what any of those failures are. A pass that touched every value
    /// up front could only get the same ordering by re-implementing each of the
    /// expander's checks, in the expander's order, for the whole template.
    /// </para>
    /// <para>
    /// An expression's strategy is resolved before its variables, because an expression
    /// carries one failure of its own: its operator may lie outside the defined set,
    /// which a custom <see cref="IUriTemplateParser"/> is free to produce. That failure
    /// belongs to the expression, so it is due at the expression's own position, ahead of
    /// every value the expression names.
    /// </para>
    /// <para>
    /// A variable is snapshotted at the first reference that will actually read it, which
    /// is what makes a single-pass or lazily evaluated sequence enumerate exactly once
    /// however many expressions name it, and what stops a mutable one giving two
    /// expressions two different answers. Two kinds of reference are skipped: one whose
    /// variable is already snapshotted, and one carrying a prefix modifier — the expander
    /// rejects a prefix over a composite before reading the value, so materializing it
    /// here would read a value on the way to a failure that is already certain, and a
    /// prefix over a string needs no snapshot. A name the template never references is
    /// never visited, so an unused lazy, blocking, or endless sequence is left alone.
    /// </para>
    /// </summary>
    private string ExpandCore(Dictionary<string, object?> ordinalVariables)
    {
        var sb = new System.Text.StringBuilder();

        // Allocated on first use: a template of literals, or one whose variables are all
        // strings, never needs it.
        HashSet<string>? snapshotted = null;

        foreach (var token in _tokens)
        {
            if (token is UriTemplateLiteralToken literal)
            {
                sb.Append(literal.Value);
            }
            else if (token is UriTemplateExpressionToken expression)
            {
                _ = OperatorStrategyFactory.For(expression.Operator);

                foreach (var varSpec in expression.Variables)
                {
                    var name = varSpec.Name;

                    if (varSpec.PrefixLength.HasValue
                        || (snapshotted is not null && snapshotted.Contains(name)))
                    {
                        continue;
                    }

                    if (!ordinalVariables.TryGetValue(name, out var value)
                        || value is null
                        || value is string)
                    {
                        // Undefined, null (undefined per RFC 6570 §2.3) and simple
                        // strings are already in the state the expander needs.
                        continue;
                    }

                    if (TrySnapshot(name, value, out var snapshot))
                    {
                        ordinalVariables[name] = snapshot;

                        snapshotted ??= new HashSet<string>(StringComparer.Ordinal);
                        snapshotted.Add(name);
                    }
                }

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
        // from a value until ExpandCore reaches the expression that names it.
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
    /// Tries to take an owned snapshot of a caller-supplied composite value, validating
    /// each member as it is copied.
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
    /// which branch a value takes.
    /// </para>
    /// <para>
    /// A value matching none of those shapes is handed back exactly as the caller supplied
    /// it, and nothing is reported: raising the unsupported type here would put the failure
    /// ahead of any expression the template names first. The expander names it on reaching
    /// the variable, which is where template order puts it.
    /// </para>
    /// </summary>
    /// <returns>
    /// <see langword="true"/> when <paramref name="value"/> was copied into an owned
    /// snapshot, which the caller should substitute for it; <see langword="false"/> when
    /// the value is not a shape RFC 6570 expansion supports.
    /// </returns>
    private static bool TrySnapshot(string name, object value, out object result)
    {
        // Every branch below is selected by a runtime type test, which reads nothing from
        // the value.

        if (value is IDictionary<string, string> dictionaryValue)
        {
            // Copy into a dictionary shape, not a list: UriTemplateExpander dispatches on
            // the runtime type, and its ordinal key ordering is selected by that type
            // rather than by the contents of the sequence. Flattening to
            // IEnumerable<KeyValuePair<,>> here would silently opt these values out of
            // that ordering.
            //
            // Not into a Dictionary<string, string>, though, for the reason the set
            // branch below cannot use a HashSet: that would impose the copy's own key
            // equality on entries that were never subject to it. A caller's dictionary
            // may be built with a comparer finer than ordinal and so legally hold two
            // entries whose keys have equal text but are distinct instances, which an
            // ordinal copy collapses into one — dropping a member the expander used to
            // enumerate and canonicalize. UriTemplateDictionarySnapshot keeps every
            // entry it observes, in order, and is still an IDictionary<string, string>.
            // Copying by hand also keeps a null key reported as this overload's
            // documented FormatException rather than the ArgumentNullException a
            // Dictionary<,> copy constructor would raise.
            var snapshot = new UriTemplateDictionarySnapshot();

            foreach (var entry in dictionaryValue)
            {
                ThrowIfNullPair(name, entry);

                snapshot.Append(entry);
            }

            result = snapshot;

            return true;
        }

        if (value is ISet<KeyValuePair<string, string>> setValue)
        {
            // A set has no meaningful order of its own, and the expander canonicalizes
            // one by ordinal key order — but only for a value that still presents as an
            // ISet<KeyValuePair<string, string>>. Copying into a List here, as the pairs
            // branch below does, would hand the expander an ordered sequence and make the
            // result depend on the caller's set implementation.
            //
            // A HashSet is equally wrong, for the opposite reason: it would impose
            // EqualityComparer<KeyValuePair<string, string>>.Default on the entries. A
            // caller's set may be built with a finer comparer and so legally hold two
            // entries the default relation calls equal, which the copy would silently
            // merge into one. The expander enumerated the caller's set and canonicalized
            // whatever came out of it, so the snapshot must keep every entry it observed.
            // UriTemplateSetSnapshot does both: it stores what it is given, in order, and
            // is still an ISet<KeyValuePair<string, string>>. Its own enumeration order
            // does not matter — that is canonicalized downstream, not here.
            var snapshot = new UriTemplateSetSnapshot();

            foreach (var entry in setValue)
            {
                ThrowIfNullPair(name, entry);

                snapshot.Append(entry);
            }

            result = snapshot;

            return true;
        }

        if (value is IEnumerable<KeyValuePair<string, string>> pairsValue)
        {
            var snapshot = new List<KeyValuePair<string, string>>();

            foreach (var entry in pairsValue)
            {
                ThrowIfNullPair(name, entry);

                snapshot.Add(entry);
            }

            result = snapshot;

            return true;
        }

        if (value is IEnumerable<string> listValue)
        {
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

            result = items;

            return true;
        }

        // Not a shape expansion supports. Hand the value back untouched — nothing has
        // been read from it, and nothing is reported here.
        result = value;

        return false;
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
