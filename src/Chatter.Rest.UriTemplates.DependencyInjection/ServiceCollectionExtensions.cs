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
    /// <remarks>
    /// Both registrations use TryAdd semantics, so registration is idempotent: calling this method more than
    /// once is safe and adds no duplicate registrations, and a registration for
    /// <see cref="IUriTemplateParser"/> or <see cref="IUriTemplateFactory"/> that already exists in
    /// <paramref name="services"/> is left in place. A custom <see cref="IUriTemplateParser"/> registered
    /// before this call therefore survives it, and it is the parser resolved by the default
    /// <see cref="IUriTemplateFactory"/> this method registers, because that factory resolves
    /// <see cref="IUriTemplateParser"/> from the service provider. When a custom
    /// <see cref="IUriTemplateFactory"/> is also registered before this call, that factory is left in place
    /// instead of the default one, and it is under no obligation to resolve the registered
    /// <see cref="IUriTemplateParser"/>.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddUriTemplates(this IServiceCollection services)
    {
        services.TryAddSingleton<IUriTemplateParser>(UriTemplateParser.Default);
        services.TryAddTransient<IUriTemplateFactory>(sp =>
            new UriTemplateFactory(sp.GetRequiredService<IUriTemplateParser>(), UriTemplateExpander.Default));

        return services;
    }
}
