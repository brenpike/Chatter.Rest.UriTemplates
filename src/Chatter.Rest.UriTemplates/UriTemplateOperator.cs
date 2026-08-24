namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Identifies the RFC 6570 operator of a URI template expression, which selects the
/// expansion form applied to the expression's variables.
/// </summary>
public enum UriTemplateOperator
{
    /// <summary>No operator: simple string expansion, <c>{var}</c> (RFC 6570 §3.2.2).</summary>
    None,

    /// <summary>The <c>+</c> operator: reserved expansion, <c>{+var}</c> (RFC 6570 §3.2.3).</summary>
    Plus,

    /// <summary>The <c>#</c> operator: fragment expansion, <c>{#var}</c> (RFC 6570 §3.2.4).</summary>
    Hash,

    /// <summary>The <c>.</c> operator: label expansion with dot-prefix, <c>{.var}</c> (RFC 6570 §3.2.5).</summary>
    Dot,

    /// <summary>The <c>/</c> operator: path segment expansion, <c>{/var}</c> (RFC 6570 §3.2.6).</summary>
    Slash,

    /// <summary>The <c>;</c> operator: path-style parameter expansion, <c>{;var}</c> (RFC 6570 §3.2.7).</summary>
    Semicolon,

    /// <summary>The <c>?</c> operator: form-style query expansion, <c>{?var}</c> (RFC 6570 §3.2.8).</summary>
    Query,

    /// <summary>The <c>&amp;</c> operator: form-style query continuation, <c>{&amp;var}</c> (RFC 6570 §3.2.9).</summary>
    Ampersand,
}
