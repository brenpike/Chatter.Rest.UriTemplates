using Chatter.Rest.UriTemplates;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Chatter.Rest.UriTemplates.DependencyInjection.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddUriTemplates_RegistersIUriTemplateParser()
    {
        var services = new ServiceCollection();

        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var parser = provider.GetService<IUriTemplateParser>();
        parser.Should().NotBeNull();
    }

    [Fact]
    public void AddUriTemplates_RegistersIUriTemplateFactory()
    {
        var services = new ServiceCollection();

        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetService<IUriTemplateFactory>();
        factory.Should().NotBeNull();
    }

    [Fact]
    public void AddUriTemplates_IUriTemplateParser_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IUriTemplateParser>();
        var second = provider.GetRequiredService<IUriTemplateParser>();
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void AddUriTemplates_IUriTemplateFactory_IsSingleton()
    {
        var services = new ServiceCollection();

        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IUriTemplateFactory>();
        var second = provider.GetRequiredService<IUriTemplateFactory>();
        ReferenceEquals(first, second).Should().BeTrue();
    }

    [Fact]
    public void AddUriTemplates_Factory_CreatesWorkingUriTemplate()
    {
        var services = new ServiceCollection();

        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IUriTemplateFactory>();
        var template = factory.Create("{var}");
        var result = template.Expand(("var", "hello"));
        result.Should().Be("hello");
    }

    [Fact]
    public void BackwardCompat_NewUriTemplate_WorksWithoutDI()
    {
        var template = new UriTemplate("{var}");

        var result = template.Expand(("var", "world"));

        result.Should().Be("world");
    }

    [Fact]
    public void AddUriTemplates_IsIdempotent()
    {
        var services = new ServiceCollection();

        services.AddUriTemplates();
        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var parser = provider.GetRequiredService<IUriTemplateParser>();
        parser.Should().NotBeNull();
    }

    [Fact]
    public void AddUriTemplates_CustomParser_WinsOverDefault()
    {
        var services = new ServiceCollection();
        var customParser = new StubUriTemplateParser();

        services.AddSingleton<IUriTemplateParser>(customParser);
        services.AddUriTemplates();

        using var provider = services.BuildServiceProvider();
        var resolved = provider.GetRequiredService<IUriTemplateParser>();
        resolved.Should().BeSameAs(customParser);
    }

    private sealed class StubUriTemplateParser : IUriTemplateParser
    {
        public IReadOnlyList<UriTemplateToken> Parse(string template) => Array.Empty<UriTemplateToken>();
    }
}
