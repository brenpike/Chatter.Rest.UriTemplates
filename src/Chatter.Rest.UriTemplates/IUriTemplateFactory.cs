namespace Chatter.Rest.UriTemplates;

/// <summary>
/// Creates <see cref="UriTemplate"/> instances. The dependency-injection alternative to calling
/// the <see cref="UriTemplate"/> constructor directly; available via the
/// <c>Chatter.Rest.UriTemplates.DependencyInjection</c> package's <c>AddUriTemplates</c>
/// registration.
/// </summary>
public interface IUriTemplateFactory
{
    /// <summary>
    /// Creates a <see cref="UriTemplate"/> from <paramref name="template"/>. With the default
    /// parser, this throws exactly what <c>new UriTemplate(string)</c> throws; the authoritative
    /// contract is "Constructor exceptions" in <c>docs/usage.md</c>. A custom
    /// <see cref="IUriTemplateParser"/> registration substitutes its own parse-failure behavior.
    /// </summary>
    /// <param name="template">The RFC 6570 URI template string.</param>
    /// <returns>The parsed <see cref="UriTemplate"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="template"/> is null.</exception>
    /// <seealso cref="UriTemplate"/>
    UriTemplate Create(string template);
}
