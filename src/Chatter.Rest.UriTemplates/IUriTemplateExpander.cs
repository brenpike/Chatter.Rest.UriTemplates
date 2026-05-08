namespace Chatter.Rest.UriTemplates;

internal interface IUriTemplateExpander
{
    string Expand(UriTemplateExpressionToken expression, IDictionary<string, object?> variables);
}
