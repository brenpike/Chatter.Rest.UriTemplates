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

            // Type dispatch: string first (string is IEnumerable<char>), then dict, then list, then fail.
            if (rawValue is string stringValue)
            {
                ExpandString(strategy, varSpec, varName, stringValue, parts);
            }
            else if (rawValue is IDictionary<string, string> dictValue)
            {
                // IDictionary<,> defines no enumeration order, so impose one.
                ExpandAssociativeArray(strategy, varSpec, varName, dictValue, parts, orderKeysOrdinally: true);
            }
            else if (rawValue is IEnumerable<KeyValuePair<string, string>> kvpEnumerable)
            {
                // An ordered sequence carries its own order; preserve it verbatim.
                ExpandAssociativeArray(strategy, varSpec, varName, kvpEnumerable, parts, orderKeysOrdinally: false);
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
    /// The order is derived from the supplied value's own ordering contract:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// An ordered sequence — any <see cref="IEnumerable{T}"/> of
    /// <see cref="KeyValuePair{TKey, TValue}"/> that is not an
    /// <see cref="IDictionary{TKey, TValue}"/>, such as
    /// <see cref="List{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> — has a
    /// defined enumeration order, so that order is preserved verbatim. Duplicate
    /// keys are emitted in the order supplied. This is the caller-controlled path
    /// and reproduces the RFC 6570 §3.2.1 example set exactly.
    /// </description></item>
    /// <item><description>
    /// An <see cref="IDictionary{TKey, TValue}"/> defines no enumeration order.
    /// <see cref="Dictionary{TKey, TValue}"/> in particular enumerates in hash-slot
    /// order, which diverges from insertion order once an entry has been removed and
    /// another inserted into the freed slot, so two logically identical maps would
    /// otherwise expand differently. Pairs from an
    /// <see cref="IDictionary{TKey, TValue}"/> are therefore sorted by ordinal key
    /// comparison (<see cref="string.CompareOrdinal(string, string)"/>) before
    /// expansion. Callers who need a specific pair order must supply an ordered
    /// sequence instead.
    /// </description></item>
    /// </list>
    /// <para>
    /// Ordering is applied before percent-encoding, so it depends only on the raw
    /// keys and never on the operator in effect.
    /// </para>
    /// </remarks>
    /// <param name="strategy">The operator strategy in effect for the expression.</param>
    /// <param name="varSpec">The variable specification being expanded.</param>
    /// <param name="varName">The variable name, used for named-operator output and error messages.</param>
    /// <param name="pairs">The associative-array pairs to expand.</param>
    /// <param name="parts">The accumulator that receives the formatted parts.</param>
    /// <param name="orderKeysOrdinally">
    /// <see langword="true"/> when <paramref name="pairs"/> came from an
    /// <see cref="IDictionary{TKey, TValue}"/> and must therefore be sorted by ordinal
    /// key comparison; <see langword="false"/> to preserve the supplied enumeration order.
    /// </param>
    /// <exception cref="FormatException">
    /// A prefix modifier was applied to a composite value, or a pair has a null key,
    /// an empty key, or a null value.
    /// </exception>
    private static void ExpandAssociativeArray(
        IOperatorStrategy strategy,
        UriTemplateVarSpec varSpec,
        string varName,
        IEnumerable<KeyValuePair<string, string>> pairs,
        List<string> parts,
        bool orderKeysOrdinally)
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
            if (kvp.Key.Length == 0)
            {
                throw new FormatException(
                    $"Variable '{varName}' contains an empty key. " +
                    "Associative array keys must be non-empty strings.");
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
        // Keys within an IDictionary are unique, and CompareOrdinal never returns 0
        // for distinct strings, so no two pairs can tie and the sort is total.
        if (orderKeysOrdinally)
        {
            pairList.Sort(static (left, right) => string.CompareOrdinal(left.Key, right.Key));
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
