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
    /// Verifies that <paramref name="value"/> is well-formed UTF-16 and can therefore be
    /// percent-encoded as UTF-8, without allocating an encoded result.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers that transform a value before handing it to <see cref="EncodeUnreserved"/> or
    /// <see cref="EncodeReserved"/> must call this on the ORIGINAL value first. The RFC 6570
    /// section 2.4.1 prefix modifier (<c>:N</c>) is exactly such a transform: truncating to N
    /// code points can discard an unpaired surrogate that sits beyond the prefix boundary, so a
    /// malformed value such as <c>"a\uD83Db"</c> under <c>{v:1}</c> would reach the encoder as
    /// the perfectly valid <c>"a"</c> and expand successfully instead of being rejected.
    /// Validating up front makes rejection independent of where the invalid code unit sits
    /// relative to the prefix boundary.
    /// </para>
    /// <para>
    /// This is a pure UTF-16 well-formedness scan: it accepts a high surrogate only when it is
    /// immediately followed by a low surrogate and rejects every other surrogate, which is
    /// exactly the input <see cref="StrictUtf8"/> would reject.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> contains an unpaired UTF-16 surrogate.
    /// </exception>
    internal static void ValidateEncodable(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (char.IsHighSurrogate(c))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    throw UnpairedSurrogate(i);
                }

                // Valid pair: skip the low surrogate, the two units are one code point.
                i++;
                continue;
            }

            if (char.IsLowSurrogate(c))
            {
                throw UnpairedSurrogate(i);
            }
        }
    }

    private static FormatException UnpairedSurrogate(int index, Exception? inner = null)
    {
        return new FormatException(
            $"Variable value contains an unpaired UTF-16 surrogate at index {index} and cannot be percent-encoded.",
            inner);
    }

    /// <summary>
    /// Encodes <paramref name="value"/> as UTF-8, rejecting unpaired surrogates.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="FormatException">
    /// Thrown when <paramref name="value"/> contains an unpaired UTF-16 surrogate.
    /// </exception>
    private static byte[] GetUtf8Bytes(string value)
    {
        ValidateEncodable(value);

        try
        {
            return StrictUtf8.GetBytes(value);
        }
        catch (EncoderFallbackException ex)
        {
            // Defence in depth: ValidateEncodable has already rejected this input.
            throw UnpairedSurrogate(ex.Index, ex);
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
