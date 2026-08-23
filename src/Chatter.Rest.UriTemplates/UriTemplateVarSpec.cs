namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Describes a single variable specification within a URI template expression,
/// including its name, optional prefix length modifier, and explode modifier.
/// </summary>
/// <remarks>
/// Validation is enforced by the property <c>init</c> accessors, so it applies to every
/// construction path: the positional constructor, object-initializer syntax, and
/// <c>with</c> expressions. Because an <c>init</c> accessor runs while the object is only
/// partially initialized, the mutual-exclusion rule between <see cref="PrefixLength"/> and
/// <see cref="Explode"/> is evaluated against the state that exists at the moment each
/// property is assigned. No invalid instance can be produced, but a single initializer that
/// swaps one modifier for the other must clear the outgoing modifier before setting the
/// incoming one (for example <c>spec with { PrefixLength = null, Explode = true }</c>);
/// the reverse ordering is rejected because it transiently combines both modifiers.
/// </remarks>
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

    // Positional-construction path. These initializers read the primary constructor
    // parameters (not the properties), so they do not depend on field declaration order.
    // The record's synthesized copy constructor copies these fields directly, which is safe
    // because the instance being copied was already validated; any property reassigned by an
    // object initializer or a 'with' expression is re-validated by its init accessor below.
    private readonly string _name = ValidateName(Name, nameof(Name));
    private readonly bool _explode = Explode;
    private readonly int? _prefixLength = ValidatePrefixLength(PrefixLength, Explode, nameof(PrefixLength));

    /// <inheritdoc cref="UriTemplateVarSpec(string, int?, bool)"/>
    public string Name
    {
        get => _name;
        init => _name = ValidateName(value, nameof(Name));
    }

    /// <inheritdoc cref="UriTemplateVarSpec(string, int?, bool)"/>
    public int? PrefixLength
    {
        get => _prefixLength;
        init => _prefixLength = ValidatePrefixLength(value, _explode, nameof(PrefixLength));
    }

    /// <inheritdoc cref="UriTemplateVarSpec(string, int?, bool)"/>
    public bool Explode
    {
        get => _explode;
        init
        {
            ValidateModifiersAreExclusive(_prefixLength, value, nameof(Explode));
            _explode = value;
        }
    }

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

        ValidateModifiersAreExclusive(length, explode, paramName);

        return length;
    }

    // RFC 6570 §2.4: a varspec carries at most one modifier.
    private static void ValidateModifiersAreExclusive(int? prefixLength, bool explode, string paramName)
    {
        if (prefixLength is not null && explode)
        {
            throw new ArgumentException(
                "Prefix modifier ':N' and explode modifier '*' are mutually exclusive per RFC 6570.",
                paramName);
        }
    }
}
