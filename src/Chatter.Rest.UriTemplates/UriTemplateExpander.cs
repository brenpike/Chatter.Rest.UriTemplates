using System.Globalization;
using System.Text;

namespace Chatter.Rest.UriTemplates;

internal static class UriTemplateExpander
{
    internal static string Expand(UriTemplateExpression expression, IDictionary<string, object?> variables)
    {
        var op = expression.Operator;
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
                ExpandString(op, varSpec, varName, stringValue, parts);
            }
            else if (rawValue is IDictionary<string, string> dictValue)
            {
                ExpandAssociativeArray(op, varSpec, varName, dictValue, parts);
            }
            else if (rawValue is IEnumerable<KeyValuePair<string, string>> kvpEnumerable)
            {
                ExpandAssociativeArray(op, varSpec, varName, kvpEnumerable, parts);
            }
            else if (rawValue is IEnumerable<string> listValue)
            {
                ExpandList(op, varSpec, varName, listValue, parts);
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

        var separator = GetSeparator(op);
        var joined = JoinParts(parts, separator);
        var prefix = GetPrefix(op);

        if (prefix.Length > 0)
        {
            return prefix + joined;
        }

        return joined;
    }

    private static void ExpandString(
        UriTemplateOperator op,
        UriTemplateVarSpec varSpec,
        string varName,
        string value,
        List<string> parts)
    {
        // Apply prefix truncation if specified
        if (varSpec.PrefixLength.HasValue)
        {
            value = TruncateByTextElements(value, varSpec.PrefixLength.Value);
        }

        if (value.Length == 0)
        {
            // Empty value: apply operator-specific empty-value rule
            parts.Add(FormatEmpty(op, varName));
        }
        else
        {
            // Non-empty value: encode and format
            var encoded = Encode(op, value);
            parts.Add(FormatValue(op, varName, encoded));
        }
    }

    private static void ExpandList(
        UriTemplateOperator op,
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
            ExpandListExplode(op, varName, items, parts);
        }
        else
        {
            ExpandListNoExplode(op, varName, items, parts);
        }
    }

    private static void ExpandListNoExplode(
        UriTemplateOperator op,
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
            sb.Append(Encode(op, items[i]));
        }

        var compositeValue = sb.ToString();

        // Format with operator prefix and (for named operators) the variable name
        if (IsNamedOperator(op))
        {
            parts.Add(varName + "=" + compositeValue);
        }
        else
        {
            parts.Add(compositeValue);
        }
    }

    private static void ExpandListExplode(
        UriTemplateOperator op,
        string varName,
        List<string> items,
        List<string> parts)
    {
        if (IsNamedOperator(op))
        {
            // Each member becomes varname=encodedMember, joined by operator separator.
            // ifEmp rules apply per member.
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Length == 0)
                {
                    // Empty member: apply ifEmp rule per operator.
                    // Semicolon: varname (no =)
                    // Query/Ampersand: varname=
                    if (op == UriTemplateOperator.Semicolon)
                    {
                        parts.Add(varName);
                    }
                    else
                    {
                        parts.Add(varName + "=");
                    }
                }
                else
                {
                    var encodedMember = Encode(op, items[i]);
                    parts.Add(varName + "=" + encodedMember);
                }
            }
        }
        else
        {
            // Each member becomes a value-only segment
            for (var i = 0; i < items.Count; i++)
            {
                parts.Add(Encode(op, items[i]));
            }
        }
    }

    private static void ExpandAssociativeArray(
        UriTemplateOperator op,
        UriTemplateVarSpec varSpec,
        string varName,
        IEnumerable<KeyValuePair<string, string>> pairs,
        List<string> parts)
    {
        // Prefix modifier is not applicable to composite values per RFC 6570
        if (varSpec.PrefixLength.HasValue)
        {
            throw new FormatException(
                $"Prefix modifier is not applicable to composite values per RFC 6570 (variable '{varName}').");
        }

        // Materialize to check emptiness and validate values
        var pairList = new List<KeyValuePair<string, string>>();
        foreach (var kvp in pairs)
        {
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

        if (varSpec.Explode)
        {
            ExpandAssocExplode(op, varName, pairList, parts);
        }
        else
        {
            ExpandAssocNoExplode(op, varName, pairList, parts);
        }
    }

    private static void ExpandAssocNoExplode(
        UriTemplateOperator op,
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
            sb.Append(Encode(op, pairs[i].Key));
            sb.Append(',');
            sb.Append(Encode(op, pairs[i].Value));
        }

        var compositeValue = sb.ToString();

        // Format with operator prefix and (for named operators) the variable name
        if (IsNamedOperator(op))
        {
            parts.Add(varName + "=" + compositeValue);
        }
        else
        {
            parts.Add(compositeValue);
        }
    }

    private static void ExpandAssocExplode(
        UriTemplateOperator op,
        string varName,
        List<KeyValuePair<string, string>> pairs,
        List<string> parts)
    {
        // Each pair becomes key=value, joined by operator separator.
        // For named operators with empty value: ; produces key only (no =); ?/& produce key=.
        for (var i = 0; i < pairs.Count; i++)
        {
            var encodedKey = Encode(op, pairs[i].Key);

            if (IsNamedOperator(op) && pairs[i].Value.Length == 0)
            {
                // ifEmp rules per operator
                if (op == UriTemplateOperator.Semicolon)
                {
                    parts.Add(encodedKey);
                }
                else
                {
                    parts.Add(encodedKey + "=");
                }
            }
            else
            {
                var encodedValue = Encode(op, pairs[i].Value);
                parts.Add(encodedKey + "=" + encodedValue);
            }
        }
    }

    /// <summary>
    /// Truncates a string to the specified number of Unicode text elements.
    /// Uses StringInfo to correctly handle surrogate pairs and combining sequences.
    /// </summary>
    private static string TruncateByTextElements(string value, int maxElements)
    {
        var si = new StringInfo(value);
        var textElementCount = si.LengthInTextElements;

        if (textElementCount <= maxElements)
        {
            return value;
        }

        return si.SubstringByTextElements(0, maxElements);
    }

    private static bool IsNamedOperator(UriTemplateOperator op)
    {
        return op == UriTemplateOperator.Semicolon ||
               op == UriTemplateOperator.Query ||
               op == UriTemplateOperator.Ampersand;
    }

    private static string FormatEmpty(UriTemplateOperator op, string varName)
    {
        switch (op)
        {
            case UriTemplateOperator.None:
            case UriTemplateOperator.Plus:
                return "";
            case UriTemplateOperator.Hash:
                return "";
            case UriTemplateOperator.Dot:
                return "";
            case UriTemplateOperator.Slash:
                return "";
            case UriTemplateOperator.Semicolon:
                return varName;
            case UriTemplateOperator.Query:
            case UriTemplateOperator.Ampersand:
                return varName + "=";
            default:
                return "";
        }
    }

    private static string FormatValue(UriTemplateOperator op, string varName, string encodedValue)
    {
        switch (op)
        {
            case UriTemplateOperator.None:
            case UriTemplateOperator.Plus:
            case UriTemplateOperator.Hash:
            case UriTemplateOperator.Dot:
            case UriTemplateOperator.Slash:
                return encodedValue;
            case UriTemplateOperator.Semicolon:
            case UriTemplateOperator.Query:
            case UriTemplateOperator.Ampersand:
                return varName + "=" + encodedValue;
            default:
                return encodedValue;
        }
    }

    private static string Encode(UriTemplateOperator op, string value)
    {
        switch (op)
        {
            case UriTemplateOperator.Plus:
            case UriTemplateOperator.Hash:
                return EncodeReserved(value);
            default:
                return EncodeUnreserved(value);
        }
    }

    private static string GetPrefix(UriTemplateOperator op)
    {
        switch (op)
        {
            case UriTemplateOperator.Hash: return "#";
            case UriTemplateOperator.Dot: return ".";
            case UriTemplateOperator.Slash: return "/";
            case UriTemplateOperator.Semicolon: return ";";
            case UriTemplateOperator.Query: return "?";
            case UriTemplateOperator.Ampersand: return "&";
            default: return "";
        }
    }

    private static string GetSeparator(UriTemplateOperator op)
    {
        switch (op)
        {
            case UriTemplateOperator.None:
            case UriTemplateOperator.Plus:
            case UriTemplateOperator.Hash:
                return ",";
            case UriTemplateOperator.Dot:
                return ".";
            case UriTemplateOperator.Slash:
                return "/";
            case UriTemplateOperator.Semicolon:
                return ";";
            case UriTemplateOperator.Query:
            case UriTemplateOperator.Ampersand:
                return "&";
            default:
                return ",";
        }
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

    private static string EncodeUnreserved(string value)
    {
        var sb = new StringBuilder();
        var bytes = Encoding.UTF8.GetBytes(value);
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            var c = (char)b;
            if (IsUnreservedChar(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%');
                sb.Append(b.ToString("X2"));
            }
        }
        return sb.ToString();
    }

    private static string EncodeReserved(string value)
    {
        var sb = new StringBuilder();
        var bytes = Encoding.UTF8.GetBytes(value);
        var i = 0;
        while (i < bytes.Length)
        {
            var c = (char)bytes[i];

            // Check for existing pct-encoded sequence: %XX
            if (c == '%' && i + 2 < bytes.Length && IsHexDigit((char)bytes[i + 1]) && IsHexDigit((char)bytes[i + 2]))
            {
                sb.Append((char)bytes[i]);
                sb.Append((char)bytes[i + 1]);
                sb.Append((char)bytes[i + 2]);
                i += 3;
                continue;
            }

            if (IsUnreservedChar(c) || IsReservedChar(c))
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('%');
                sb.Append(bytes[i].ToString("X2"));
            }

            i++;
        }
        return sb.ToString();
    }

    private static bool IsUnreservedChar(char c)
    {
        return (c >= 'A' && c <= 'Z') ||
               (c >= 'a' && c <= 'z') ||
               (c >= '0' && c <= '9') ||
               c == '-' || c == '.' || c == '_' || c == '~';
    }

    private static bool IsReservedChar(char c)
    {
        // gen-delims: : / ? # [ ] @
        // sub-delims: ! $ & ' ( ) * + , ; =
        // Note: '%' is NOT included here. Valid pct-encoded triplets (%XX)
        // are handled earlier in EncodeReserved; bare '%' must be encoded as %25.
        switch (c)
        {
            case ':':
            case '/':
            case '?':
            case '#':
            case '[':
            case ']':
            case '@':
            case '!':
            case '$':
            case '&':
            case '\'':
            case '(':
            case ')':
            case '*':
            case '+':
            case ',':
            case ';':
            case '=':
                return true;
            default:
                return false;
        }
    }

    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }
}
