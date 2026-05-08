namespace Chatter.Rest.UriTemplates;

internal sealed class UriTemplateFactory : IUriTemplateFactory
{
    internal static readonly UriTemplateFactory Default = new UriTemplateFactory(UriTemplateParser.Default, UriTemplateExpander.Default);

    private readonly IUriTemplateParser _parser;
    private readonly IUriTemplateExpander _expander;

    internal UriTemplateFactory(IUriTemplateParser parser, IUriTemplateExpander expander)
    {
        _parser = parser;
        _expander = expander;
    }

    public UriTemplate Create(string template)
    {
        if (template is null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        return new UriTemplate(template, _parser, _expander);
    }
}
