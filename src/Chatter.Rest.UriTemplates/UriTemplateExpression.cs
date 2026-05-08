namespace Chatter.Rest.UriTemplates;

internal sealed record UriTemplateExpression(
	UriTemplateOperator Operator,
	IReadOnlyList<UriTemplateVarSpec> Variables
);
