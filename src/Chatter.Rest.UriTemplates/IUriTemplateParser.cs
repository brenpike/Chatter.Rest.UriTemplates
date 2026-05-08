namespace Chatter.Rest.UriTemplates;

public interface IUriTemplateParser
{
    IReadOnlyList<UriTemplateToken> Parse(string template);
}
