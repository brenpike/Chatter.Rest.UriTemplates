using System.Text;

namespace Chatter.Rest.UriTemplates;

internal static class UriTemplateEncoder
{
    internal static string EncodeUnreserved(string value)
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

    internal static string EncodeReserved(string value)
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
