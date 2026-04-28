using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	public class UriTemplateTupleOverloadTests
	{
		// ----------------------------------------------------------------
		// Expand(params (string Key, object? Value)[])
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_ObjectTuple_NullVariables_ThrowsArgumentNullException()
		{
			var template = new UriTemplate("{var}");
			var act = () => template.Expand((ValueTuple<string, object?>[])null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Expand_ObjectTuple_EmptyVariables_AllUndefined()
		{
			var template = new UriTemplate("{var}");
			template.Expand(Array.Empty<(string, object?)>()).Should().Be("");
		}

		[Fact]
		public void Expand_ObjectTuple_StringValue_SimpleExpansion()
		{
			var template = new UriTemplate("{var}");
			template.Expand(("var", (object?)"hello")).Should().Be("hello");
		}

		[Fact]
		public void Expand_ObjectTuple_ListValue_SimpleExpansion()
		{
			var template = new UriTemplate("{list}");
			template.Expand(("list", (object?)new[] { "a", "b", "c" })).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_ObjectTuple_ListValue_ExplodeExpansion()
		{
			var template = new UriTemplate("{list*}");
			template.Expand(("list", (object?)new[] { "a", "b", "c" })).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_ObjectTuple_DictionaryValue_ExplodeExpansion()
		{
			var template = new UriTemplate("{keys*}");
			var dict = new Dictionary<string, string> { ["k"] = "v" };
			template.Expand(("keys", (object?)dict)).Should().Be("k=v");
		}

		[Fact]
		public void Expand_ObjectTuple_NullValue_TreatedAsUndefined()
		{
			var template = new UriTemplate("{var}");
			template.Expand(("var", (object?)null)).Should().Be("");
		}

		[Fact]
		public void Expand_ObjectTuple_MixedKinds()
		{
			var template = new UriTemplate("{name}/{list*}");
			template.Expand(
				("name", (object?)"alice"),
				("list", (object?)new[] { "x", "y" })
			).Should().Be("alice/x,y");
		}

		[Fact]
		public void Expand_ObjectTuple_DuplicateKey_FirstWins()
		{
			var template = new UriTemplate("{var}");
			template.Expand(
				("var", (object?)"first"),
				("var", (object?)"second")
			).Should().Be("first");
		}

		[Fact]
		public void Expand_ObjectTuple_UriTemplateValueInstance_ThrowsFormatException()
		{
			var template = new UriTemplate("{var}");
			var act = () => template.Expand(("var", (object?)UriTemplateValue.From("x")));
			act.Should().Throw<FormatException>();
		}

		// ----------------------------------------------------------------
		// Expand(params (string Key, UriTemplateValue Value)[])
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_UriTemplateValueTuple_NullVariables_ThrowsArgumentNullException()
		{
			var template = new UriTemplate("{var}");
			var act = () => template.Expand((ValueTuple<string, UriTemplateValue>[])null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Expand_UriTemplateValueTuple_EmptyVariables_AllUndefined()
		{
			var template = new UriTemplate("{var}");
			template.Expand(Array.Empty<(string, UriTemplateValue)>()).Should().Be("");
		}

		[Fact]
		public void Expand_UriTemplateValueTuple_StringValue()
		{
			var template = new UriTemplate("{var}");
			template.Expand(("var", UriTemplateValue.From("hello"))).Should().Be("hello");
		}

		[Fact]
		public void Expand_UriTemplateValueTuple_ListValue()
		{
			var template = new UriTemplate("{list}");
			template.Expand(("list", UriTemplateValue.From(new[] { "a", "b", "c" }))).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_UriTemplateValueTuple_DictionaryValue()
		{
			var template = new UriTemplate("{keys*}");
			var dict = new Dictionary<string, string> { ["k"] = "v" };
			template.Expand(("keys", UriTemplateValue.From(dict))).Should().Be("k=v");
		}

		[Fact]
		public void Expand_UriTemplateValueTuple_MixedKinds()
		{
			var template = new UriTemplate("{name}/{list*}/{keys*}");
			var dict = new Dictionary<string, string> { ["a"] = "1" };
			template.Expand(
				("name", (UriTemplateValue)UriTemplateValue.From("alice")),
				("list", (UriTemplateValue)UriTemplateValue.From(new[] { "x", "y" })),
				("keys", (UriTemplateValue)UriTemplateValue.From(dict))
			).Should().Be("alice/x,y/a=1");
		}

		[Fact]
		public void Expand_UriTemplateValueTuple_DuplicateKey_FirstWins()
		{
			var template = new UriTemplate("{var}");
			template.Expand(
				("var", UriTemplateValue.From("first")),
				("var", UriTemplateValue.From("second"))
			).Should().Be("first");
		}
	}
}
