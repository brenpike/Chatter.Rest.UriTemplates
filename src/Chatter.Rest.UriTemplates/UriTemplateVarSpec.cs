namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Describes a single variable specification within a URI template expression,
/// including its name, optional prefix length modifier, and explode modifier.
/// </summary>
/// <param name="Name">The variable name. Must not be null or empty.</param>
/// <param name="PrefixLength">
/// The optional prefix modifier length. When supplied it must be between
/// <see cref="MinPrefixLength"/> and <see cref="MaxPrefixLength"/> inclusive
/// (RFC 6570 §2.4.1: max-length = %x31-39 0*3DIGIT).
/// </param>
/// <param name="Explode">The explode modifier. Mutually exclusive with <paramref name="PrefixLength"/>.</param>
/// <exception cref="ArgumentNullException"><paramref name="Name"/> is null.</exception>
/// <exception cref="ArgumentException">
/// <paramref name="Name"/> is empty, or <paramref name="PrefixLength"/> is combined with
/// <paramref name="Explode"/>.
/// </exception>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="PrefixLength"/> is outside the range 1-9999.
/// </exception>
public sealed record UriTemplateVarSpec(string Name, int? PrefixLength, bool Explode)
{
    /// <summary>Smallest prefix modifier length permitted by RFC 6570 §2.4.1.</summary>
    internal const int MinPrefixLength = 1;

    /// <summary>Largest prefix modifier length permitted by RFC 6570 §2.4.1.</summary>
    internal const int MaxPrefixLength = 9999;

    /// <inheritdoc cref="UriTemplateVarSpec(string, int?, bool)"/>
    public string Name { get; init; } = ValidateName(Name, nameof(Name));

    /// <inheritdoc cref="UriTemplateVarSpec(string, int?, bool)"/>
    public int? PrefixLength { get; init; } = ValidatePrefixLength(PrefixLength, Explode, nameof(PrefixLength));

    private static string ValidateName(string name, string paramName)
    {
        if (name is null)
        {
            throw new ArgumentNullException(paramName);
        }

        if (name.Length == 0)
        {
            throw new ArgumentException("Variable name must not be empty.", paramName);
        }

        return name;
    }

    private static int? ValidatePrefixLength(int? prefixLength, bool explode, string paramName)
    {
        if (prefixLength is null)
        {
            return null;
        }

        var length = prefixLength.Value;

        if (length < MinPrefixLength || length > MaxPrefixLength)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                length,
                $"Prefix modifier length must be between {MinPrefixLength} and {MaxPrefixLength}.");
        }

        // RFC 6570 §2.4: a varspec carries at most one modifier.
        if (explode)
        {
            throw new ArgumentException(
                "Prefix modifier ':N' and explode modifier '*' are mutually exclusive per RFC 6570.",
                paramName);
        }

        return length;
    }
}
