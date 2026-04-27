using System.Text;

namespace Chatter.Rest.UriTemplates;

internal static class UriTemplateParser
{
    private static readonly char[] OperatorChars = { '+', '#', '.', '/', ';', '?', '&' };
    private static readonly char[] ReservedOperatorChars = { '=', ',', '!', '@', '|' };

    internal static IReadOnlyList<object> Parse(string template)
    {
        var tokens = new List<object>();
        var pos = 0;

        while (pos < template.Length)
        {
            var openIndex = template.IndexOf('{', pos);

            if (openIndex < 0)
            {
                // No more expressions; rest is literal
                tokens.Add(ProcessLiteral(template.Substring(pos)));
                break;
            }

            // Emit literal text before the '{'
            if (openIndex > pos)
            {
                tokens.Add(ProcessLiteral(template.Substring(pos, openIndex - pos)));
            }

            // Find matching '}'
            var closeIndex = template.IndexOf('}', openIndex + 1);

            if (closeIndex < 0)
            {
                throw new FormatException("Unclosed '{' in URI template.");
            }

            // Check for nested '{' between openIndex+1 and closeIndex
            var nestedOpen = template.IndexOf('{', openIndex + 1);
            if (nestedOpen >= 0 && nestedOpen < closeIndex)
            {
                throw new FormatException("Nested '{' are not allowed in URI templates.");
            }

            var content = template.Substring(openIndex + 1, closeIndex - openIndex - 1);

            if (content.Length == 0)
            {
                throw new FormatException("Empty expression '{}' is not valid in a URI template.");
            }

            // Check for reserved future operators (=, ,, !, @, |) per RFC 6570 §2.2
            if (Array.IndexOf(ReservedOperatorChars, content[0]) >= 0)
            {
                throw new NotSupportedException(
                    $"Operator '{content[0]}' is reserved for future use and is not supported.");
            }

            // Determine operator
            var op = UriTemplateOperator.None;
            var varsPart = content;

            if (Array.IndexOf(OperatorChars, content[0]) >= 0)
            {
                op = MapOperator(content[0]);
                varsPart = content.Substring(1);

                // Detect double operator (e.g., {??x})
                if (varsPart.Length > 0 &&
                    (Array.IndexOf(OperatorChars, varsPart[0]) >= 0 ||
                     Array.IndexOf(ReservedOperatorChars, varsPart[0]) >= 0))
                {
                    throw new FormatException("Double operator in expression is not valid.");
                }
            }

            // Split variable names on ','
            var rawNames = varsPart.Split(',');
            var variables = new List<UriTemplateVarSpec>(rawNames.Length);

            for (var i = 0; i < rawNames.Length; i++)
            {
                var name = rawNames[i];

                // Detect Level 4 modifiers before varname validation
                var colonIdx = name.IndexOf(':');
                var starIdx = name.IndexOf('*');

                // Mutual-exclusion check: both prefix and explode present
                if (colonIdx >= 0 && starIdx >= 0)
                {
                    throw new FormatException(
                        "Prefix modifier ':N' and explode modifier '*' are mutually exclusive per RFC 6570.");
                }

                if (colonIdx >= 0)
                {
                    var suffix = name.Substring(colonIdx + 1);

                    if (suffix.Length == 0)
                    {
                        throw new FormatException(
                            "Prefix modifier ':' must be followed by a length (1-9999).");
                    }

                    // Check that all characters after ':' are digits
                    for (var ci = 0; ci < suffix.Length; ci++)
                    {
                        if (suffix[ci] < '0' || suffix[ci] > '9')
                        {
                            throw new FormatException(
                                $"Prefix modifier length must be numeric, got ':{suffix}'.");
                        }
                    }

                    // RFC 6570 §2.4.1 ABNF: max-length = %x31-39 0*3DIGIT
                    // The first digit must be 1-9; leading zeros are not permitted.
                    if (suffix[0] == '0')
                    {
                        throw new FormatException(
                            $"Prefix modifier length must not have leading zeros, got ':{suffix}'.");
                    }

                    // Parse the numeric value and validate range 1-9999
                    if (!int.TryParse(suffix, out var prefixLen) || prefixLen < 1 || prefixLen > 9999)
                    {
                        throw new FormatException(
                            $"Prefix modifier length must be between 1 and 9999, got ':{suffix}'.");
                    }

                    var varName = name.Substring(0, colonIdx);

                    if (varName.Length == 0)
                    {
                        throw new FormatException("Empty variable name in expression.");
                    }

                    ValidateVarName(varName);

                    variables.Add(new UriTemplateVarSpec(varName, prefixLen, false));
                    continue;
                }

                if (starIdx >= 0)
                {
                    // '*' is only valid at the very end of the varspec
                    if (starIdx != name.Length - 1)
                    {
                        throw new FormatException(
                            $"Explode modifier '*' must appear at the end of the variable name, not at position {starIdx} in '{name}'.");
                    }

                    var varName = name.Substring(0, starIdx);

                    if (varName.Length == 0)
                    {
                        throw new FormatException("Empty variable name in expression.");
                    }

                    ValidateVarName(varName);

                    variables.Add(new UriTemplateVarSpec(varName, null, true));
                    continue;
                }

                if (name.Length == 0)
                {
                    throw new FormatException("Empty variable name in expression.");
                }

                ValidateVarName(name);

                variables.Add(new UriTemplateVarSpec(name, null, false));
            }

            tokens.Add(new UriTemplateExpression(op, variables));

            pos = closeIndex + 1;
        }

        return tokens;
    }

    /// <summary>
    /// Validates a variable name per RFC 6570 §2.3.
    /// varname = varchar *( ["."] varchar )
    /// varchar = ALPHA / DIGIT / "_" / pct-encoded
    /// </summary>
    private static void ValidateVarName(string name)
    {
        var i = 0;

        while (i < name.Length)
        {
            var c = name[i];

            if (c == ' ' || c == '\t' || c == '\r' || c == '\n')
            {
                throw new FormatException($"Whitespace is not allowed in variable name '{name}'.");
            }

            if (c == '.')
            {
                // Consecutive dots
                if (i + 1 < name.Length && name[i + 1] == '.')
                {
                    throw new FormatException($"Consecutive dots are not allowed in variable name '{name}'.");
                }

                // Trailing dot
                if (i == name.Length - 1)
                {
                    throw new FormatException($"Trailing dot is not allowed in variable name '{name}'.");
                }

                i++;
                continue;
            }

            if (c == '%')
            {
                // Must be followed by exactly two hex digits
                if (i + 2 < name.Length && IsHexDigit(name[i + 1]) && IsHexDigit(name[i + 2]))
                {
                    i += 3;
                    continue;
                }

                throw new FormatException($"Invalid percent-encoded triplet in variable name '{name}'.");
            }

            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_')
            {
                i++;
                continue;
            }

            throw new FormatException($"Invalid character '{c}' in variable name '{name}'.");
        }
    }

    /// <summary>
    /// Validates and encodes a literal segment per RFC 6570 §2.1/§3.1.
    /// Non-ASCII chars are UTF-8 pct-encoded. Spaces, lone '}', and
    /// invalid percent triplets are rejected with FormatException.
    /// Valid pct-encoded triplets and RFC 3986 unreserved/reserved chars pass through.
    /// </summary>
    private static string ProcessLiteral(string raw)
    {
        var sb = new StringBuilder(raw.Length);
        var i = 0;

        while (i < raw.Length)
        {
            var c = raw[i];

            if (c == '}')
            {
                throw new FormatException("Invalid literal character '}' in URI template.");
            }

            if (c == ' ')
            {
                throw new FormatException("Invalid literal character ' ' in URI template.");
            }

            if (c == '%')
            {
                // Must be followed by exactly two hex digits
                if (i + 2 < raw.Length && IsHexDigit(raw[i + 1]) && IsHexDigit(raw[i + 2]))
                {
                    sb.Append(raw[i]);
                    sb.Append(raw[i + 1]);
                    sb.Append(raw[i + 2]);
                    i += 3;
                    continue;
                }

                throw new FormatException("Invalid percent-encoded triplet in literal.");
            }

            if (c > 127)
            {
                // Non-ASCII: encode as UTF-8 pct-encoded bytes
                var charCount = char.IsHighSurrogate(c) && i + 1 < raw.Length ? 2 : 1;
                var chars = raw.ToCharArray(i, charCount);
                var bytes = Encoding.UTF8.GetBytes(chars, 0, charCount);
                foreach (var b in bytes)
                {
                    sb.Append('%');
                    sb.Append(b.ToString("X2"));
                }
                i += charCount;
                continue;
            }

            // All other ASCII chars that are valid URI literals: pass through unchanged
            sb.Append(c);
            i++;
        }

        return sb.ToString();
    }

    private static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }

    private static UriTemplateOperator MapOperator(char c)
    {
        switch (c)
        {
            case '+': return UriTemplateOperator.Plus;
            case '#': return UriTemplateOperator.Hash;
            case '.': return UriTemplateOperator.Dot;
            case '/': return UriTemplateOperator.Slash;
            case ';': return UriTemplateOperator.Semicolon;
            case '?': return UriTemplateOperator.Query;
            case '&': return UriTemplateOperator.Ampersand;
            default: return UriTemplateOperator.None;
        }
    }
}
