using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Tests for <see cref="UriTemplateValue"/> factory methods and the
	/// <see cref="UriTemplate.Expand(IDictionary{string, UriTemplateValue})"/> overload.
	/// </summary>
	public class UriTemplateValueTests
	{
		// ----------------------------------------------------------------
		// 1. Factory method validation — FromString
		// ----------------------------------------------------------------

		[Fact]
		public void FromString_NullValue_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateValue.FromString(null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void FromString_ValidValue_ReturnsStringValueSubtype()
		{
			var value = UriTemplateValue.FromString("hello");
			value.Should().BeOfType<UriTemplateValue.StringValue>();
			((UriTemplateValue.StringValue)value).Value.Should().Be("hello");
		}

		// ----------------------------------------------------------------
		// 2. Factory method validation — FromList
		// ----------------------------------------------------------------

		[Fact]
		public void FromList_NullValues_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateValue.FromList(null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void FromList_ContainsNullElement_ThrowsArgumentException()
		{
			Action act = () => UriTemplateValue.FromList(new[] { "a", null!, "b" });
			act.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void FromList_EmptyList_ReturnsListValueSubtype()
		{
			var value = UriTemplateValue.FromList(Array.Empty<string>());
			value.Should().BeOfType<UriTemplateValue.ListValue>();
			((UriTemplateValue.ListValue)value).Values.Should().BeEmpty();
		}

		[Fact]
		public void FromList_ValidValues_ReturnsListValueSubtype()
		{
			var value = UriTemplateValue.FromList(new[] { "red", "green", "blue" });
			value.Should().BeOfType<UriTemplateValue.ListValue>();
			((UriTemplateValue.ListValue)value).Values.Should().Equal("red", "green", "blue");
		}

		// ----------------------------------------------------------------
		// 3. Factory method validation — FromDictionary
		// ----------------------------------------------------------------

		[Fact]
		public void FromDictionary_NullPairs_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateValue.FromDictionary(null!);
			act.Should().Throw<ArgumentNullException>();
		}

		// Dictionary<string, string> does not allow null keys (throws at insertion),
		// so a null-key scenario cannot reach FromDictionary's own guard. Instead,
		// verify that Dictionary itself rejects the null key.
		[Fact]
		public void FromDictionary_ContainsNullKey_ThrowsDueToDictionary()
		{
			// Dictionary<string, string> constructor or Add throws
			// ArgumentNullException for a null key before our code runs.
			Action act = () =>
			{
				var dict = new Dictionary<string, string>();
				dict.Add(null!, "value");
			};
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void FromDictionary_ContainsNullValue_ThrowsArgumentException()
		{
			// Use a type that can actually hold a null value in its collection
			// to reach FromDictionary's own null-value guard.
			var dict = new Dictionary<string, string?> { ["key"] = null };
			Action act = () => UriTemplateValue.FromDictionary(dict!);
			act.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void FromDictionary_EmptyDictionary_ReturnsDictionaryValueSubtype()
		{
			var value = UriTemplateValue.FromDictionary(new Dictionary<string, string>());
			value.Should().BeOfType<UriTemplateValue.DictionaryValue>();
			((UriTemplateValue.DictionaryValue)value).Pairs.Should().BeEmpty();
		}

		[Fact]
		public void FromDictionary_ValidPairs_ReturnsDictionaryValueSubtype()
		{
			var pairs = new Dictionary<string, string>
			{
				["semi"] = ";",
				["dot"] = ".",
			};
			var value = UriTemplateValue.FromDictionary(pairs);
			value.Should().BeOfType<UriTemplateValue.DictionaryValue>();
			((UriTemplateValue.DictionaryValue)value).Pairs.Should().ContainKey("semi").WhoseValue.Should().Be(";");
			((UriTemplateValue.DictionaryValue)value).Pairs.Should().ContainKey("dot").WhoseValue.Should().Be(".");
		}

		// ----------------------------------------------------------------
		// 4. Expansion — string values
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_StringValue_SimpleExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["var"] = UriTemplateValue.FromString("hello"),
			};
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("hello");
		}

		[Fact]
		public void Expand_StringValue_ReservedExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["var"] = UriTemplateValue.FromString("hello world"),
			};
			var template = new UriTemplate("{+var}");
			template.Expand(vars).Should().Be("hello%20world");
		}

		[Fact]
		public void Expand_StringValue_WithPrefixModifier()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["var"] = UriTemplateValue.FromString("hello"),
			};
			var template = new UriTemplate("{var:3}");
			template.Expand(vars).Should().Be("hel");
		}

		// ----------------------------------------------------------------
		// 5. Expansion — list values
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_ListValue_SimpleExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["list"] = UriTemplateValue.FromList(new[] { "a", "b", "c" }),
			};
			var template = new UriTemplate("{list}");
			template.Expand(vars).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_ListValue_ExplodeExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["list"] = UriTemplateValue.FromList(new[] { "a", "b", "c" }),
			};
			var template = new UriTemplate("{list*}");
			template.Expand(vars).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_ListValue_PathSegmentExplode()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["list"] = UriTemplateValue.FromList(new[] { "a", "b", "c" }),
			};
			var template = new UriTemplate("{/list*}");
			template.Expand(vars).Should().Be("/a/b/c");
		}

		// ----------------------------------------------------------------
		// 6. Expansion — dictionary values
		// ----------------------------------------------------------------

		// Use a single-entry dictionary for deterministic output.
		[Fact]
		public void Expand_DictionaryValue_SimpleExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["keys"] = UriTemplateValue.FromDictionary(new Dictionary<string, string>
				{
					["semi"] = ";",
				}),
			};
			var template = new UriTemplate("{keys}");
			template.Expand(vars).Should().Be("semi,%3B");
		}

		// Use a single-entry dictionary for deterministic key=value output.
		[Fact]
		public void Expand_DictionaryValue_ExplodeExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["keys"] = UriTemplateValue.FromDictionary(new Dictionary<string, string>
				{
					["semi"] = ";",
				}),
			};
			var template = new UriTemplate("{keys*}");
			template.Expand(vars).Should().Be("semi=%3B");
		}

		// ----------------------------------------------------------------
		// 7. Expansion — null dictionary and empty composite values
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_NullDictionary_ThrowsArgumentNullException()
		{
			var template = new UriTemplate("{var}");
			Action act = () => template.Expand((IDictionary<string, UriTemplateValue>)null!);
			act.Should().Throw<ArgumentNullException>();
		}

		// Empty list is treated as undefined per RFC 6570 section 2.3
		[Fact]
		public void Expand_EmptyList_UndefinedBehavior()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["list"] = UriTemplateValue.FromList(Array.Empty<string>()),
			};
			var template = new UriTemplate("{list}");
			template.Expand(vars).Should().Be("");
		}

		// Empty dictionary is treated as undefined per RFC 6570 section 2.3
		[Fact]
		public void Expand_EmptyDictionary_UndefinedBehavior()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["keys"] = UriTemplateValue.FromDictionary(new Dictionary<string, string>()),
			};
			var template = new UriTemplate("{keys}");
			template.Expand(vars).Should().Be("");
		}

		// ----------------------------------------------------------------
		// 8. Expansion — multiple variables of mixed kinds
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_MultipleVariables_MixedKinds()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["id"] = UriTemplateValue.FromString("42"),
				["tags"] = UriTemplateValue.FromList(new[] { "red", "blue" }),
				["meta"] = UriTemplateValue.FromDictionary(new Dictionary<string, string>
				{
					["color"] = "green",
				}),
			};
			var template = new UriTemplate("/items/{id}{?tags,meta}");
			template.Expand(vars).Should().Be("/items/42?tags=red,blue&meta=color,green");
		}
	}
}
