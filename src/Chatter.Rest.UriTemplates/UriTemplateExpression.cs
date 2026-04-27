namespace Chatter.Rest.UriTemplates;

internal sealed record UriTemplateVarSpec(
	string Name,
	int? PrefixLength,
	bool Explode
);

internal sealed record UriTemplateExpression(
	UriTemplateOperator Operator,
	IReadOnlyList<UriTemplateVarSpec> Variables
);
