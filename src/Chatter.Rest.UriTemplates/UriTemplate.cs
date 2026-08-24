namespace Chatter.Rest.UriTemplates;

/// <summary>
/// An RFC 6570 URI Template, parsed eagerly at construction and expanded against
/// caller-supplied variables (Levels 1–4).
/// </summary>
public sealed class UriTemplate
{
    private readonly IReadOnlyList<UriTemplateToken> _tokens;
    private readonly IUriTemplateExpander _expander;

    /// <summary>
    /// Parses <paramref name="template"/> eagerly, so every parse failure surfaces at
    /// construction time — never later, at <c>Expand</c>. The authoritative statement of
    /// this exception contract is "Constructor exceptions" in <c>docs/usage.md</c>.
    /// </summary>
    /// <param name="template">The RFC 6570 URI template string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when the template is malformed: an unclosed <c>{</c>, a nested <c>{</c>, an empty
    /// expression, a double operator, an invalid variable name, an invalid prefix or explode
    /// modifier, or invalid literal text. See "Constructor exceptions" in <c>docs/usage.md</c>
    /// for the full enumeration.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when an expression starts with one of the operators RFC 6570 §2.2 reserves for
    /// future use: <c>=</c>, <c>,</c>, <c>!</c>, <c>@</c>, or <c>|</c>. See "Constructor
    /// exceptions" in <c>docs/usage.md</c>.
    /// </exception>
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
    /// modifier is the sharpest case: it decides whether a reference is legal at all over
    /// a composite value, so a prefix appearing after construction would turn a template
    /// that expands into one that fails, and a prefix disappearing would turn a template
    /// that fails without reading anything into one that reads a value the constructed
    /// form never asked for.
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
    /// <exception cref="FormatException">
    /// Thrown when a variable value contains an unpaired UTF-16 surrogate and cannot be
    /// percent-encoded. That message reports the character index within the value, not the
    /// variable name, and never quotes value text — see "Exception message content" in
    /// <c>docs/usage.md</c>.
    /// </exception>
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
    /// <para>
    /// Composite values must be finite sequences: a value the template references is enumerated
    /// exactly once and fully drained within a single <c>Expand</c> call, so an endless sequence
    /// supplied for a variable the template names means <c>Expand</c> never returns. See "Values
    /// must be finite sequences" in <c>docs/usage.md</c>.
    /// </para>
    /// <para>
    /// Expansion-time exception messages identify the failing variable by name and, for an
    /// associative-array member, the offending key — variable values are never quoted. The one
    /// exception: the unpaired-surrogate message reports the character index within the value
    /// instead of the variable name. See "Exception message content" in <c>docs/usage.md</c>.
    /// </para>
    /// </summary>
    /// <param name="variables">A dictionary mapping variable names to values of the supported types listed above.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when a variable value is not one of the supported types, when a prefix modifier
    /// (e.g. <c>{var:3}</c>) is applied to a composite value, when a composite value contains a
    /// null element, key, or value, or when a string value, list element, or associative-array
    /// key or value contains an unpaired UTF-16 surrogate and cannot be percent-encoded.
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
            ordinal[kvp.Key] = Memoize(kvp.Key, kvp.Value);
        }

        // Memoizing is a runtime type test and nothing more; no value is read here, and
        // none is read at all until the expression that names it is expanded.
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
    /// <para>
    /// The finite-sequence contract applies at construction here rather than at expansion:
    /// <see cref="UriTemplateValue.From(IEnumerable{string})"/> materializes its sequence eagerly,
    /// so an endless sequence hangs <c>From</c>, never this method. See "Values must be finite
    /// sequences" in <c>docs/usage.md</c>.
    /// </para>
    /// <para>
    /// Expansion-time exception messages identify the failing variable by name and never quote
    /// variable values; the unpaired-surrogate message reports the character index within the
    /// value instead of the variable name. See "Exception message content" in <c>docs/usage.md</c>.
    /// </para>
    /// </summary>
    /// <param name="variables">A dictionary mapping variable names to <see cref="UriTemplateValue"/> instances.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when a prefix modifier (e.g. <c>{var:3}</c>) is applied to a <see cref="ListValue"/>
    /// or <see cref="DictionaryValue"/>, or when a string held by a value contains an unpaired
    /// UTF-16 surrogate and cannot be percent-encoded. Unsupported types and null members are
    /// unreachable through this overload, because <see cref="UriTemplateValue.From(string)"/> and
    /// its sibling factories validate their contents at construction.
    /// </exception>
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
                : Memoize(kvp.Key, UriTemplateExpander.MapValue(kvp.Key, kvp.Value));
        }

        return ExpandCore(mapped);
    }

    /// <summary>
    /// Canonical expansion path. Walks the template's tokens once, appending each literal
    /// and delegating each expression to <see cref="UriTemplateExpander"/>.
    /// <para>
    /// It reads no variable and prepares nothing. Every composite value was wrapped in a
    /// memoizing view of itself on the way in (see <see cref="Memoize"/>), and such a view
    /// materializes on its first enumeration — which is the expander enumerating it, at
    /// the expression that names it. So whichever failure the template reaches first is
    /// the one raised: an operator outside the defined set, an unsupported type, a prefix
    /// modifier over a composite, a null member, a string that cannot be encoded. None of
    /// them is anticipated here, and none needs to be, because nothing runs ahead of the
    /// expander to get in front of them.
    /// </para>
    /// <para>
    /// That makes the guarantee unconditional rather than expression-granular. A value the
    /// expander never reaches is never read, whether it sits in a later expression or
    /// alongside the failing variable in the same one: <c>{bad,later}</c> reports
    /// <c>bad</c> and leaves <c>later</c> untouched, because materializing <c>later</c>
    /// would take an enumeration that never happens.
    /// </para>
    /// </summary>
    private string ExpandCore(Dictionary<string, object?> variables)
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
                sb.Append(_expander.Expand(expression, variables));
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
    /// <exception cref="FormatException">
    /// Thrown when a variable value contains an unpaired UTF-16 surrogate and cannot be
    /// percent-encoded. That message reports the character index within the value, not the
    /// variable name, and never quotes value text — see "Exception message content" in
    /// <c>docs/usage.md</c>.
    /// </exception>
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
    /// <para>
    /// Composite values must be finite sequences: a value the template references is enumerated
    /// exactly once and fully drained within a single <c>Expand</c> call, so an endless sequence
    /// supplied for a variable the template names means <c>Expand</c> never returns. See "Values
    /// must be finite sequences" in <c>docs/usage.md</c>.
    /// </para>
    /// <para>
    /// Expansion-time exception messages identify the failing variable by name and, for an
    /// associative-array member, the offending key — variable values are never quoted. The one
    /// exception: the unpaired-surrogate message reports the character index within the value
    /// instead of the variable name. See "Exception message content" in <c>docs/usage.md</c>.
    /// </para>
    /// </summary>
    /// <param name="variables">Name/value tuples mapping variable names to values of the supported types listed above. Neither the array nor any key may be null.</param>
    /// <returns>The expanded URI string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when an entry has a null key; the message names the entry index.</exception>
    /// <exception cref="FormatException">
    /// Thrown when a variable value is not one of the supported types, when a prefix modifier
    /// (e.g. <c>{var:3}</c>) is applied to a composite value, when a composite value contains a
    /// null element, key, or value, or when a string value, list element, or associative-array
    /// key or value contains an unpaired UTF-16 surrogate and cannot be percent-encoded.
    /// </exception>
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
                dict[key] = Memoize(key, value);
            }
        }

        // The tuple array's order is the caller's, not the template's; memoizing reads
        // nothing, so no value is touched until the expression that names it is expanded.
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
    /// Wraps a caller-supplied composite value in a memoizing view of itself: one that
    /// reads the caller's sequence on its first enumeration, validating each member as it
    /// goes, and replays the recorded members on every enumeration after that.
    /// <para>
    /// Deferring the read is what makes the value's first enumeration coincide with the
    /// expander's, and so puts every failure in template order without this method knowing
    /// what any of those failures are. Wrapping itself is a runtime type test and nothing
    /// else, so it is safe to do for every value the caller supplies, including the ones
    /// the template never mentions: an unused lazy, blocking, or endless sequence is
    /// wrapped and then simply never enumerated.
    /// </para>
    /// <para>
    /// Memoizing is what stops a single-pass or lazily evaluated sequence being drained by
    /// the first expression that names it, and what stops a mutable one giving two
    /// expressions two different answers. The view is built once per expansion, so however
    /// many expressions name the variable, the caller's sequence is read exactly once.
    /// </para>
    /// <para>
    /// Members are validated during the read rather than after it. Reading first and
    /// leaving validation to the expander would carry on past the first invalid member, so
    /// a null key, null value, or null element followed by a throwing, blocking, or
    /// endless remainder would never be reported. Validating in step makes the first
    /// invalid member the observable failure and stops the read there. Every message below
    /// is the one <see cref="UriTemplateExpander"/> produces for the same input, naming the
    /// same variable.
    /// </para>
    /// <para>
    /// The type dispatch mirrors <see cref="UriTemplateExpander"/>, which selects its
    /// associative-array ordering by runtime type: each branch produces a view that still
    /// satisfies the interface the expander tests for, so no wrapper can change which
    /// branch a value takes.
    /// </para>
    /// <para>
    /// A value matching none of those shapes is handed back exactly as the caller supplied
    /// it, and nothing is reported: raising the unsupported type here would put the failure
    /// ahead of any expression the template names first. The expander names it on reaching
    /// the variable, which is where template order puts it.
    /// </para>
    /// </summary>
    /// <returns>
    /// A memoizing view of <paramref name="value"/> when it is a composite shape RFC 6570
    /// expansion supports, and <paramref name="value"/> itself otherwise.
    /// </returns>
    private static object? Memoize(string name, object? value)
    {
        // Undefined values, nulls (undefined per RFC 6570 2.3) and simple strings are
        // already in the state the expander needs, and none of them can be read twice.
        if (value is null || value is string)
        {
            return value;
        }

        // Every branch below is selected by a runtime type test, which reads nothing from
        // the value.

        if (value is IDictionary<string, string> dictionaryValue)
        {
            // Wrap in a dictionary shape, not a list: UriTemplateExpander dispatches on
            // the runtime type, and its ordinal key ordering is selected by that type
            // rather than by the contents of the sequence. Handing over a bare
            // IEnumerable<KeyValuePair<,>> here would silently opt these values out of
            // that ordering.
            //
            // Not a Dictionary<string, string>, though, for the reason the set branch
            // below cannot use a HashSet: that would impose the copy's own key equality on
            // entries that were never subject to it. A caller's dictionary may be built
            // with a comparer finer than ordinal and so legally hold two entries whose keys
            // have equal text but are distinct instances, which an ordinal copy collapses
            // into one — dropping a member the expander used to enumerate and canonicalize.
            // UriTemplateMemoizedDictionary keeps every entry it observes, in order, and is
            // still an IDictionary<string, string>.
            return new UriTemplateMemoizedDictionary(MemoizePairs(name, dictionaryValue));
        }

        if (value is ISet<KeyValuePair<string, string>> setValue)
        {
            // A set has no meaningful order of its own, and the expander canonicalizes one
            // by ordinal key order — but only for a value that still presents as an
            // ISet<KeyValuePair<string, string>>. Handing over the bare pair sequence, as
            // the branch below does, would give the expander an ordered sequence and make
            // the result depend on the caller's set implementation.
            //
            // A HashSet is equally wrong, for the opposite reason: it would impose
            // EqualityComparer<KeyValuePair<string, string>>.Default on the entries. A
            // caller's set may be built with a finer comparer and so legally hold two
            // entries the default relation calls equal, which the copy would silently merge
            // into one. The expander enumerated the caller's set and canonicalized whatever
            // came out of it, so the view must keep every entry it observed.
            // UriTemplateMemoizedSet does both: it records what it is given, in order, and
            // is still an ISet<KeyValuePair<string, string>>. Its own enumeration order
            // does not matter — that is canonicalized downstream, not here.
            return new UriTemplateMemoizedSet(MemoizePairs(name, setValue));
        }

        if (value is IEnumerable<KeyValuePair<string, string>> pairsValue)
        {
            // Any other pair sequence: the caller's order is the contract, so the view
            // presents as nothing more than the IEnumerable<KeyValuePair<,>> it wraps.
            return MemoizePairs(name, pairsValue);
        }

        if (value is IEnumerable<string> listValue)
        {
            return new UriTemplateMemoizedSequence<string>(name, listValue, ThrowIfNullElement);
        }

        // Not a shape expansion supports. Hand the value back untouched — nothing has
        // been read from it, and nothing is reported here.
        return value;
    }

    /// <summary>
    /// Builds the memoizing view shared by all three associative-array shapes, which
    /// differ only in the interface they must still present to the expander.
    /// </summary>
    private static UriTemplateMemoizedSequence<KeyValuePair<string, string>> MemoizePairs(
        string name,
        IEnumerable<KeyValuePair<string, string>> pairs) =>
        new UriTemplateMemoizedSequence<KeyValuePair<string, string>>(name, pairs, ThrowIfNullPair);

    /// <summary>
    /// Rejects a null element of a list value, with the message
    /// <c>UriTemplateExpander.ExpandList</c> uses for the same element.
    /// </summary>
    private static void ThrowIfNullElement(string name, string item)
    {
        if (item is null)
        {
            throw new FormatException(
                $"Variable '{name}' contains a null element. List elements must be non-null strings.");
        }
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

    /// <summary>
    /// Returns the distinct variable names the template references, in the order the template
    /// first references them. Duplicate references are deduplicated by ordinal (case-sensitive)
    /// comparison, so names differing only in case are distinct entries.
    /// </summary>
    /// <returns>The deduplicated variable names in first-occurrence template order.</returns>
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
