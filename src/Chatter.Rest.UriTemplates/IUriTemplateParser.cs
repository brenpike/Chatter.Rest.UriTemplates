namespace Chatter.Rest.UriTemplates;

public interface IUriTemplateParser
{
    IReadOnlyList<object> Parse(string template);
}
