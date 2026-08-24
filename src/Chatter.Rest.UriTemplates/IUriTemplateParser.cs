namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Parses an RFC 6570 URI template string into the token list that <see cref="UriTemplate"/>
/// expands. This is the library's parsing extension point: registering a custom implementation
/// (for example via the <c>Chatter.Rest.UriTemplates.DependencyInjection</c> package's
/// <c>AddUriTemplates</c> registration) replaces the default parser.
/// </summary>
public interface IUriTemplateParser
{
    /// <summary>
    /// Parses <paramref name="template"/> into a list of literal and expression tokens.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementer contract: an implementation must return a non-null token list.
    /// <see cref="UriTemplate"/> throws <see cref="InvalidOperationException"/> naming the
    /// parser type when an implementation returns null.
    /// </para>
    /// <para>
    /// Parse-failure behavior is implementation-defined. The default implementation throws
    /// <see cref="ArgumentNullException"/> for a null template, <see cref="FormatException"/>
    /// for a malformed template, and <see cref="NotSupportedException"/> for an operator
    /// RFC 6570 reserves for future use, per the constructor exception contract documented
    /// under "Constructor exceptions" in <c>docs/usage.md</c>. A custom implementation
    /// substitutes its own parse-failure behavior, so callers of a custom parser cannot rely
    /// on the default contract.
    /// </para>
    /// </remarks>
    /// <param name="template">The RFC 6570 URI template string to parse.</param>
    /// <returns>The parsed token list. Implementations must never return null.</returns>
    IReadOnlyList<UriTemplateToken> Parse(string template);
}
