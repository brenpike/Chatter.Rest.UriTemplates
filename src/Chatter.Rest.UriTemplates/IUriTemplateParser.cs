namespace Chatter.Rest.UriTemplates;

internal interface IUriTemplateParser
{
    IReadOnlyList<object> Parse(string template);
}
