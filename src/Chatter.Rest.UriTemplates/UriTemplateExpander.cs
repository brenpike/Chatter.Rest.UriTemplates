using System.Text;

namespace Chatter.Rest.UriTemplates;

internal sealed class UriTemplateExpander : IUriTemplateExpander
{
    internal static readonly UriTemplateExpander Default = new UriTemplateExpander();

    internal static object? MapValue(string key, UriTemplateValue value)
    {
        if (value is null)
        {
            throw new ArgumentException(
                $"Variable '{key}' has a null UriTemplateValue. Use UriTemplateValue.From(value) or omit the key for undefined variables.",
                nameof(value));
        }

        return value.ToRawValue();
    }

    public string Expand(UriTemplateExpressionToken expression, IDictionary<string, object?> variables)
    {
        var strategy = OperatorStrategyFactory.For(expression.Operator);
        var parts = new List<string>();

        foreach (var varSpec in expression.Variables)
        {
            var varName = varSpec.Name;

            if (!variables.TryGetValue(varName, out var rawValue) || rawValue is null)
            {
                // Undefined or null: omit per RFC 6570 §2.3
                continue;
            }

            // Type dispatch: string first (string is IEnumerable<char>), then the two
            // associative-array shapes — ordered pair lists, then unordered pair
            // enumerables (IDictionary<,> included) — then list, then fail.
            // See ExpandAssociativeArray's remarks for the ordering contract.
            if (rawValue is string stringValue)
            {
                ExpandString(strategy, varSpec, varName, stringValue, parts);
            }
            else if (rawValue is IList<KeyValuePair<string, string>> pairList)
            {
                // Index-addressable, so the caller defined the order; preserve it verbatim.
                // Covers List<KVP>, KeyValuePair<,>[], ImmutableArray/ImmutableList<KVP>,
                // ReadOnlyCollection<KVP>.
                ExpandAssociativeArray(strategy, varSpec, varName, pairList, parts, canonicalizePairOrder: false);
            }
            else if (rawValue is IReadOnlyList<KeyValuePair<string, string>> readOnlyPairList)
            {
                // Also index-addressable; same reasoning as IList<KVP>.
                ExpandAssociativeArray(strategy, varSpec, varName, readOnlyPairList, parts, canonicalizePairOrder: false);
            }
            else if (rawValue is IEnumerable<KeyValuePair<string, string>> kvpEnumerable)
            {
                // No ordering contract (IDictionary<,>, FrozenDictionary, HashSet<KVP>,
                // LINQ iterators, custom enumerables): canonicalize the pair order.
                ExpandAssociativeArray(strategy, varSpec, varName, kvpEnumerable, parts, canonicalizePairOrder: true);
            }
            else if (rawValue is IEnumerable<string> listValue)
            {
                ExpandList(strategy, varSpec, varName, listValue, parts);
            }
            else
            {
                throw new FormatException(
                    $"Variable '{varName}' has unsupported type '{rawValue.GetType().FullName}'. " +
                    "Expected string, IEnumerable<string>, IDictionary<string, string>, or IEnumerable<KeyValuePair<string, string>>.");
            }
        }

        if (parts.Count == 0)
        {
            return "";
        }

        var joined = JoinParts(parts, strategy.Separator);
        var prefix = strategy.Prefix;

        if (prefix.Length > 0)
        {
            return prefix + joined;
        }

        return joined;
    }

    private static void ExpandString(
        IOperatorStrategy strategy,
        UriTemplateVarSpec varSpec,
        string varName,
        string value,
        List<string> parts)
    {
        // Apply prefix truncation if specified
        if (varSpec.PrefixLength.HasValue)
        {
            value = TruncateByCodePoints(value, varSpec.PrefixLength.Value);
        }

        if (value.Length == 0)
        {
            // Empty value: apply operator-specific empty-value rule
            parts.Add(strategy.FormatEmpty(varName));
        }
        else
        {
            // Non-empty value: encode and format
            var encoded = strategy.Encode(value);
            parts.Add(strategy.FormatValue(varName, encoded));
        }
    }

    private static void ExpandList(
        IOperatorStrategy strategy,
        UriTemplateVarSpec varSpec,
        string varName,
        IEnumerable<string> list,
        List<string> parts)
    {
        // Prefix modifier is not applicable to composite values per RFC 6570
        if (varSpec.PrefixLength.HasValue)
        {
            throw new FormatException(
                $"Prefix modifier is not applicable to composite values per RFC 6570 (variable '{varName}').");
        }

        // Materialize the list to check for emptiness and validate elements
        var items = new List<string>();
        foreach (var item in list)
        {
            if (item is null)
            {
                throw new FormatException(
                    $"Variable '{varName}' contains a null element. List elements must be non-null strings.");
            }
            items.Add(item);
        }

        // Empty list -> undefined per RFC 6570 §2.3
        if (items.Count == 0)
        {
            return;
        }

        if (varSpec.Explode)
        {
            ExpandListExplode(strategy, varName, items, parts);
        }
        else
        {
            ExpandListNoExplode(strategy, varName, items, parts);
        }
    }

    private static void ExpandListNoExplode(
        IOperatorStrategy strategy,
        string varName,
        List<string> items,
        List<string> parts)
    {
        // Encode each member, comma-join into one composite value
        var sb = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(strategy.Encode(items[i]));
        }

        var compositeValue = sb.ToString();

        // Format with operator prefix and (for named operators) the variable name
        if (strategy.IsNamed)
        {
            parts.Add(varName + "=" + compositeValue);
        }
        else
        {
            parts.Add(compositeValue);
        }
    }

    private static void ExpandListExplode(
        IOperatorStrategy strategy,
        string varName,
        List<string> items,
        List<string> parts)
    {
        if (strategy.IsNamed)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Length == 0)
                {
                    parts.Add(strategy.FormatEmpty(varName));
                }
                else
                {
                    parts.Add(strategy.FormatValue(varName, strategy.Encode(items[i])));
                }
            }
        }
        else
        {
            for (var i = 0; i < items.Count; i++)
            {
                parts.Add(strategy.Encode(items[i]));
            }
        }
    }

    /// <summary>
    /// Expands an associative-array value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Pair ordering policy.</b> RFC 6570 does not mandate an order for
    /// associative-array pairs; this library nonetheless guarantees that a given
    /// logical map always expands to the same URI, so that expansion results are
    /// usable as cache keys, in signed URLs, and in tests.
    /// </para>
    /// <para>
    /// The order is derived from whether the supplied value's own type carries an
    /// ordering contract. The dispatch in <see cref="Expand"/> classifies the value
    /// by exactly two type tests, in this order:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Ordered — preserved verbatim.</b> A value that implements
    /// <see cref="IList{T}"/> or <see cref="IReadOnlyList{T}"/> of
    /// <see cref="KeyValuePair{TKey, TValue}"/> is index-addressable, so its element
    /// order is defined by the caller and is a property the caller controls. That
    /// order is used exactly as supplied, duplicate keys included. This covers
    /// <see cref="List{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/>, arrays of
    /// <see cref="KeyValuePair{TKey, TValue}"/>, <c>ImmutableArray</c>,
    /// <c>ImmutableList</c>, and <c>ReadOnlyCollection</c>. This is the
    /// caller-controlled path and reproduces the RFC 6570 §3.2.1 example set exactly.
    /// </description></item>
    /// <item><description>
    /// <b>Unordered — canonicalized.</b> Every other
    /// <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> has no
    /// ordering contract, so its pairs are sorted before expansion. This covers
    /// <see cref="IDictionary{TKey, TValue}"/> implementations
    /// (<see cref="Dictionary{TKey, TValue}"/>, <c>FrozenDictionary</c>,
    /// <c>ImmutableDictionary</c>, and the sorted dictionaries, all of which reach this
    /// branch), <c>HashSet</c> of <see cref="KeyValuePair{TKey, TValue}"/>, LINQ
    /// iterators, and any custom enumerable. <see cref="Dictionary{TKey, TValue}"/> in
    /// particular enumerates in hash-slot order, which diverges from insertion order
    /// once an entry has been removed and another inserted into the freed slot, and a
    /// hash-based set can reorder between processes, so two logically identical maps
    /// would otherwise expand differently.
    /// </description></item>
    /// </list>
    /// <para>
    /// The canonical order is ordinal by key
    /// (<see cref="string.CompareOrdinal(string, string)"/>), with an ordinal comparison
    /// of the value as a tie-break so that an unordered container holding duplicate keys
    /// still has a total, input-order-independent order. Keys within an
    /// <see cref="IDictionary{TKey, TValue}"/> are unique, so for a dictionary the
    /// tie-break never applies and the order is purely ordinal by key. Callers who need
    /// a specific pair order — including one that repeats a key — must supply an ordered
    /// list rather than an unordered container.
    /// </para>
    /// <para>
    /// Ordering is applied before percent-encoding, so it depends only on the raw
    /// keys and values and never on the operator in effect.
    /// </para>
    /// <para>
    /// An empty member name is permitted. RFC 6570 §2.3 models associative-array members
    /// as (name, value) string pairs and treats only a composite with zero members, or one
    /// whose member names are all associated with undefined values, as undefined; the
    /// <c>varchar</c> grammar constrains template variable names, not member names.
    /// </para>
    /// </remarks>
    /// <param name="strategy">The operator strategy in effect for the expression.</param>
    /// <param name="varSpec">The variable specification being expanded.</param>
    /// <param name="varName">The variable name, used for named-operator output and error messages.</param>
    /// <param name="pairs">The associative-array pairs to expand.</param>
    /// <param name="parts">The accumulator that receives the formatted parts.</param>
    /// <param name="canonicalizePairOrder">
    /// <see langword="true"/> when <paramref name="pairs"/> came from a container with no
    /// ordering contract and must therefore be sorted into the canonical order;
    /// <see langword="false"/> when it came from an index-addressable list whose order the
    /// caller defined and which must be preserved verbatim.
    /// </param>
    /// <exception cref="FormatException">
    /// A prefix modifier was applied to a composite value, or a pair has a null key
    /// or a null value.
    /// </exception>
    private static void ExpandAssociativeArray(
        IOperatorStrategy strategy,
        UriTemplateVarSpec varSpec,
        string varName,
        IEnumerable<KeyValuePair<string, string>> pairs,
        List<string> parts,
        bool canonicalizePairOrder)
    {
        // Prefix modifier is not applicable to composite values per RFC 6570
        if (varSpec.PrefixLength.HasValue)
        {
            throw new FormatException(
                $"Prefix modifier is not applicable to composite values per RFC 6570 (variable '{varName}').");
        }

        // Materialize to check emptiness and validate keys/values
        var pairList = new List<KeyValuePair<string, string>>();
        foreach (var kvp in pairs)
        {
            if (kvp.Key is null)
            {
                throw new FormatException(
                    $"Variable '{varName}' contains a null key. " +
                    "Associative array keys must be non-null strings.");
            }
            if (kvp.Value is null)
            {
                throw new FormatException(
                    $"Variable '{varName}' contains a null value for key '{kvp.Key}'. " +
                    "Associative array values must be non-null strings.");
            }
            pairList.Add(kvp);
        }

        // Empty associative array -> undefined per RFC 6570 §2.3
        if (pairList.Count == 0)
        {
            return;
        }

        // Impose a deterministic order on inputs that define none (see remarks).
        // Key first, value as tie-break, so the comparison is a total order even when an
        // unordered container holds duplicate keys and the sort itself is not stable.
        if (canonicalizePairOrder)
        {
            pairList.Sort(static (left, right) =>
            {
                var byKey = string.CompareOrdinal(left.Key, right.Key);
                return byKey != 0 ? byKey : string.CompareOrdinal(left.Value, right.Value);
            });
        }

        if (varSpec.Explode)
        {
            ExpandAssocExplode(strategy, varName, pairList, parts);
        }
        else
        {
            ExpandAssocNoExplode(strategy, varName, pairList, parts);
        }
    }

    private static void ExpandAssocNoExplode(
        IOperatorStrategy strategy,
        string varName,
        List<KeyValuePair<string, string>> pairs,
        List<string> parts)
    {
        // Flatten to alternating key,value,key,value,...
        // Each key and value individually encoded, comma-joined
        var sb = new StringBuilder();
        for (var i = 0; i < pairs.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }
            sb.Append(strategy.Encode(pairs[i].Key));
            sb.Append(',');
            sb.Append(strategy.Encode(pairs[i].Value));
        }

        var compositeValue = sb.ToString();

        // Format with operator prefix and (for named operators) the variable name
        if (strategy.IsNamed)
        {
            parts.Add(varName + "=" + compositeValue);
        }
        else
        {
            parts.Add(compositeValue);
        }
    }

    private static void ExpandAssocExplode(
        IOperatorStrategy strategy,
        string varName,
        List<KeyValuePair<string, string>> pairs,
        List<string> parts)
    {
        for (var i = 0; i < pairs.Count; i++)
        {
            var encodedKey = strategy.Encode(pairs[i].Key);

            if (strategy.IsNamed && pairs[i].Value.Length == 0)
            {
                parts.Add(strategy.FormatEmpty(encodedKey));
            }
            else
            {
                var encodedValue = strategy.Encode(pairs[i].Value);
                parts.Add(encodedKey + "=" + encodedValue);
            }
        }
    }

    /// <summary>
    /// Truncates a string to the specified number of Unicode code points.
    /// RFC 6570 §2.4.1 specifies prefix length in characters (Unicode code points),
    /// not grapheme clusters (text elements).
    /// Walks the UTF-16 string treating a valid high+low surrogate pair as one code point.
    /// An unpaired surrogate (high without matching low, or lone low) counts as one code point,
    /// which is consistent with Rune.DecodeFromUtf16 fallback semantics.
    /// Works on both net8.0 and netstandard2.0 (no System.Text.Rune dependency).
    /// </summary>
    private static string TruncateByCodePoints(string value, int maxCodePoints)
    {
        if (maxCodePoints <= 0) return string.Empty;
        if (string.IsNullOrEmpty(value)) return value;

        var i = 0;
        var count = 0;
        while (i < value.Length && count < maxCodePoints)
        {
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i += 2;
            }
            else
            {
                i += 1;
            }
            count++;
        }
        return value.Substring(0, i);
    }

    private static string JoinParts(List<string> parts, string separator)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }
            sb.Append(parts[i]);
        }
        return sb.ToString();
    }

}
