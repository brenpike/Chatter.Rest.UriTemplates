using Chatter.Rest.UriTemplates;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering URI template services with an <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers URI template parsing and factory services. <see cref="IUriTemplateParser"/> is registered as a
    /// singleton. <see cref="IUriTemplateFactory"/> is registered as transient so that the resolving scope's
    /// service provider is used at each resolution, making custom parser lifetimes (scoped or transient) safe
    /// when resolved from the correct scope.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddUriTemplates(this IServiceCollection services)
    {
        services.TryAddSingleton<IUriTemplateParser>(UriTemplateParser.Default);
        services.TryAddTransient<IUriTemplateFactory>(sp =>
            new UriTemplateFactory(sp.GetRequiredService<IUriTemplateParser>(), UriTemplateExpander.Default));

        return services;
    }
}
