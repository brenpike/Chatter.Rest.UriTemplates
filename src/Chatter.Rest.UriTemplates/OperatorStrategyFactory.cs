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

    /// <summary>
    /// Resolves the expansion strategy for <paramref name="op"/>.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="op"/> is not a defined <see cref="UriTemplateOperator"/> value.
    /// </exception>
    internal static IOperatorStrategy For(UriTemplateOperator op)
    {
        if (!_strategies.TryGetValue(op, out var strategy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(op),
                op,
                $"No expansion strategy is defined for URI template operator '{op}'.");
        }

        return strategy;
    }
}
