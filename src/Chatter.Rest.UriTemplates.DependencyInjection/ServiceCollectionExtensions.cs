using Chatter.Rest.UriTemplates;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering URI template services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers URI template parsing and factory services as singletons.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddUriTemplates(this IServiceCollection services)
    {
        services.TryAddSingleton<IUriTemplateParser>(UriTemplateParser.Default);
        services.TryAddSingleton<IUriTemplateFactory>(sp =>
            new UriTemplateFactory(sp.GetRequiredService<IUriTemplateParser>(), UriTemplateExpander.Default));

        return services;
    }
}
