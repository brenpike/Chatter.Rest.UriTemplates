namespace Chatter.Rest.UriTemplates;

internal interface IUriTemplateExpander
{
    string Expand(UriTemplateExpression expression, IDictionary<string, object?> variables);
}
