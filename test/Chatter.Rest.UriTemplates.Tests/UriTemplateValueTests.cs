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
		// 1. Factory method validation — From (string)
		// ----------------------------------------------------------------

		[Fact]
		public void From_String_NullValue_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateValue.From((string)null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void From_String_ValidValue_ReturnsStringValueSubtype()
		{
			var value = UriTemplateValue.From("hello");
			value.Should().BeOfType<StringValue>();
			((StringValue)value).Value.Should().Be("hello");
		}

		// ----------------------------------------------------------------
		// 2. Factory method validation — From (list)
		// ----------------------------------------------------------------

		[Fact]
		public void From_List_NullValues_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateValue.From((IEnumerable<string>)null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void From_List_ContainsNullElement_ThrowsArgumentException()
		{
			Action act = () => UriTemplateValue.From(new[] { "a", null!, "b" });
			act.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void From_List_EmptyList_ReturnsListValueSubtype()
		{
			var value = UriTemplateValue.From(Array.Empty<string>());
			value.Should().BeOfType<ListValue>();
			((ListValue)value).Values.Should().BeEmpty();
		}

		[Fact]
		public void From_List_ValidValues_ReturnsListValueSubtype()
		{
			var value = UriTemplateValue.From(new[] { "red", "green", "blue" });
			value.Should().BeOfType<ListValue>();
			((ListValue)value).Values.Should().Equal("red", "green", "blue");
		}

		// ----------------------------------------------------------------
		// 3. Factory method validation — From (dictionary)
		// ----------------------------------------------------------------

		[Fact]
		public void From_Dictionary_NullPairs_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateValue.From((IDictionary<string, string>)null!);
			act.Should().Throw<ArgumentNullException>();
		}

		// Dictionary<string, string> does not allow null keys (throws at insertion),
		// so a null-key scenario cannot reach From's own guard. Instead,
		// verify that Dictionary itself rejects the null key.
		[Fact]
		public void From_Dictionary_ContainsNullKey_ThrowsDueToDictionary()
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
		public void From_Dictionary_ContainsNullValue_ThrowsArgumentException()
		{
			// Use a type that can actually hold a null value in its collection
			// to reach From's own null-value guard.
			var dict = new Dictionary<string, string?> { ["key"] = null };
			Action act = () => UriTemplateValue.From(dict!);
			act.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void From_Dictionary_EmptyDictionary_ReturnsDictionaryValueSubtype()
		{
			var value = UriTemplateValue.From(new Dictionary<string, string>());
			value.Should().BeOfType<DictionaryValue>();
			((DictionaryValue)value).Pairs.Should().BeEmpty();
		}

		[Fact]
		public void From_Dictionary_ValidPairs_ReturnsDictionaryValueSubtype()
		{
			var pairs = new Dictionary<string, string>
			{
				["semi"] = ";",
				["dot"] = ".",
			};
			var value = UriTemplateValue.From(pairs);
			value.Should().BeOfType<DictionaryValue>();
			((DictionaryValue)value).Pairs.Should().ContainKey("semi").WhoseValue.Should().Be(";");
			((DictionaryValue)value).Pairs.Should().ContainKey("dot").WhoseValue.Should().Be(".");
		}

		// ----------------------------------------------------------------
		// 4. Expansion — string values
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_StringValue_SimpleExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["var"] = UriTemplateValue.From("hello"),
			};
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("hello");
		}

		[Fact]
		public void Expand_StringValue_ReservedExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["var"] = UriTemplateValue.From("hello world"),
			};
			var template = new UriTemplate("{+var}");
			template.Expand(vars).Should().Be("hello%20world");
		}

		[Fact]
		public void Expand_StringValue_WithPrefixModifier()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["var"] = UriTemplateValue.From("hello"),
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
				["list"] = UriTemplateValue.From(new[] { "a", "b", "c" }),
			};
			var template = new UriTemplate("{list}");
			template.Expand(vars).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_ListValue_ExplodeExpansion()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["list"] = UriTemplateValue.From(new[] { "a", "b", "c" }),
			};
			var template = new UriTemplate("{list*}");
			template.Expand(vars).Should().Be("a,b,c");
		}

		[Fact]
		public void Expand_ListValue_PathSegmentExplode()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["list"] = UriTemplateValue.From(new[] { "a", "b", "c" }),
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
				["keys"] = UriTemplateValue.From(new Dictionary<string, string>
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
				["keys"] = UriTemplateValue.From(new Dictionary<string, string>
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
				["list"] = UriTemplateValue.From(Array.Empty<string>()),
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
				["keys"] = UriTemplateValue.From(new Dictionary<string, string>()),
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
				["id"] = UriTemplateValue.From("42"),
				["tags"] = UriTemplateValue.From(new[] { "red", "blue" }),
				["meta"] = UriTemplateValue.From(new Dictionary<string, string>
				{
					["color"] = "green",
				}),
			};
			var template = new UriTemplate("/items/{id}{?tags,meta}");
			template.Expand(vars).Should().Be("/items/42?tags=red,blue&meta=color,green");
		}
	}
}
