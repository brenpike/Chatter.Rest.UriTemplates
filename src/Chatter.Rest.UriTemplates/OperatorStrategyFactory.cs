namespace Chatter.Rest.UriTemplates;

internal static class OperatorStrategyFactory
{
    private static readonly Dictionary<UriTemplateOperator, IOperatorStrategy> _strategies =
        new Dictionary<UriTemplateOperator, IOperatorStrategy>
        {
            { UriTemplateOperator.None, new NoneOperatorStrategy() },
            { UriTemplateOperator.Plus, new PlusOperatorStrategy() },
            { UriTemplateOperator.Hash, new HashOperatorStrategy() },
            { UriTemplateOperator.Dot, new DotOperatorStrategy() },
            { UriTemplateOperator.Slash, new SlashOperatorStrategy() },
            { UriTemplateOperator.Semicolon, new SemicolonOperatorStrategy() },
            { UriTemplateOperator.Query, new QueryOperatorStrategy() },
            { UriTemplateOperator.Ampersand, new AmpersandOperatorStrategy() },
        };

    internal static IOperatorStrategy For(UriTemplateOperator op) => _strategies[op];
}
