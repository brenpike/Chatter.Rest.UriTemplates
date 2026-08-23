using System.Text;

namespace Chatter.Rest.UriTemplates;

internal static class UriTemplateEncoder
{
    /// <summary>
    /// UTF-8 encoder that throws on invalid input instead of substituting U+FFFD.
    /// Without this, an unpaired UTF-16 surrogate in a variable value would be silently
    /// mangled into the replacement character, collapsing distinct invalid inputs onto the
    /// same URI. The library's policy is to reject invalid input, not to repair it.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

    /// <summary>
    /// Encodes <paramref name="value"/> as UTF-8, rejecting unpaired surrogates.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> contains an unpaired UTF-16 surrogate.
    /// </exception>
    private static byte[] GetUtf8Bytes(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        try
        {
            return StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException ex)
        {
            throw new FormatException(
                $"Variable value contains an unpaired UTF-16 surrogate at index {ex.Index} and cannot be percent-encoded.",
                ex);
        }
    }

    /// <summary>
    /// Percent-encodes every character that is not an RFC 3986 unreserved character.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> contains an unpaired UTF-16 surrogate.
    /// </exception>
    internal static string EncodeUnreserved(string value)
    {
        var sb = new StringBuilder();
        var bytes = GetUtf8Bytes(value);
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

    /// <summary>
    /// Percent-encodes every character that is not an RFC 3986 unreserved or reserved character,
    /// passing existing percent-encoded triplets through unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> contains an unpaired UTF-16 surrogate.
    /// </exception>
    internal static string EncodeReserved(string value)
    {
        var sb = new StringBuilder();
        var bytes = GetUtf8Bytes(value);
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

    internal static bool IsUnreservedChar(char c)
    {
        return (c >= 'A' && c <= 'Z') ||
               (c >= 'a' && c <= 'z') ||
               (c >= '0' && c <= '9') ||
               c == '-' || c == '.' || c == '_' || c == '~';
    }

    internal static bool IsReservedChar(char c)
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

    internal static bool IsHexDigit(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f');
    }
}
