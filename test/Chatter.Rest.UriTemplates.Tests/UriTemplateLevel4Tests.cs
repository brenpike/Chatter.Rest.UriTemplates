using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// RFC 6570 Level 4 expansion tests covering prefix modifiers, list values,
	/// list-explode, associative arrays, and associative-array-explode across
	/// all eight operators.
	///
	/// Variable values use RFC 6570 §3.2.1 standard set. Associative array tests
	/// use <see cref="List{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/>
	/// for deterministic enumeration order. Where a <see cref="Dictionary{TKey, TValue}"/>
	/// is exercised intentionally (D5 risk), assertions accept all valid orderings
	/// via <c>Should().BeOneOf(...)</c>.
	///
	/// References: docs/test-plan.md §6, RFC 6570 §3.2.
	/// </summary>
	public class UriTemplateLevel4Tests
	{
		/// <summary>
		/// RFC 6570 §3.2.1 standard variable values for Level 4.
		/// Uses <see cref="IDictionary{TKey, TValue}"/> of <see cref="string"/>
		/// to <see cref="object"/> to support composite values.
		/// </summary>
		private static IDictionary<string, object?> MakeVariables()
		{
			return new Dictionary<string, object?>
			{
				["var"] = "value",
				["hello"] = "Hello World!",
				["half"] = "50%",
				["path"] = "/foo/bar",
				["list"] = new List<string> { "red", "green", "blue" },
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("semi", ";"),
					new("dot", "."),
					new("comma", ","),
				},
				["empty_keys"] = new List<KeyValuePair<string, string>>(),
			};
		}

		// ----------------------------------------------------------------
		// 6.1 Prefix modifiers
		// ----------------------------------------------------------------

		// §6.1 Prefix_Simple: {var:3} -> val
		[Fact]
		public void Prefix_Simple()
		{
			var template = new UriTemplate("{var:3}");
			template.Expand(MakeVariables()).Should().Be("val");
		}

		// §6.1 Prefix_LongerThanValue: {var:30} -> value
		[Fact]
		public void Prefix_LongerThanValue()
		{
			var template = new UriTemplate("{var:30}");
			template.Expand(MakeVariables()).Should().Be("value");
		}

		// §6.1 Prefix_Reserved: {+path:6}/here -> /foo/b/here
		[Fact]
		public void Prefix_Reserved()
		{
			var template = new UriTemplate("{+path:6}/here");
			template.Expand(MakeVariables()).Should().Be("/foo/b/here");
		}

		// §6.1 Prefix_Fragment: {#path:6}/here -> #/foo/b/here
		[Fact]
		public void Prefix_Fragment()
		{
			var template = new UriTemplate("{#path:6}/here");
			template.Expand(MakeVariables()).Should().Be("#/foo/b/here");
		}

		// §6.1 Prefix_Label: X{.var:3} -> X.val
		[Fact]
		public void Prefix_Label()
		{
			var template = new UriTemplate("X{.var:3}");
			template.Expand(MakeVariables()).Should().Be("X.val");
		}

		// §6.1 Prefix_Path: {/var:1,var} -> /v/value
		[Fact]
		public void Prefix_Path()
		{
			var template = new UriTemplate("{/var:1,var}");
			template.Expand(MakeVariables()).Should().Be("/v/value");
		}

		// §6.1 Prefix_PathWithPctEncoding: {/list*,path:4} -> /red/green/blue/%2Ffoo
		[Fact]
		public void Prefix_PathWithPctEncoding()
		{
			var template = new UriTemplate("{/list*,path:4}");
			template.Expand(MakeVariables()).Should().Be("/red/green/blue/%2Ffoo");
		}

		// §6.1 Prefix_Semicolon: {;hello:5} -> ;hello=Hello
		[Fact]
		public void Prefix_Semicolon()
		{
			var template = new UriTemplate("{;hello:5}");
			template.Expand(MakeVariables()).Should().Be(";hello=Hello");
		}

		// §6.1 Prefix_Query: {?var:3} -> ?var=val
		[Fact]
		public void Prefix_Query()
		{
			var template = new UriTemplate("{?var:3}");
			template.Expand(MakeVariables()).Should().Be("?var=val");
		}

		// §6.1 Prefix_Ampersand: {&var:3} -> &var=val
		[Fact]
		public void Prefix_Ampersand()
		{
			var template = new UriTemplate("{&var:3}");
			template.Expand(MakeVariables()).Should().Be("&var=val");
		}

		// §6.1 Prefix_UnicodeCountsCodePoints: {var:1} with emoji -> single text element
		[Fact]
		public void Prefix_UnicodeCountsCodePoints()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = "\U0001F600x",
			};
			var template = new UriTemplate("{var:1}");
			template.Expand(vars).Should().Be("%F0%9F%98%80");
		}

		// §6.1 Prefix with combining marks: truncation counts Unicode code points, not grapheme clusters.
		// Input "éfg" is 4 code points (e, combining acute, f, g) but 3 grapheme clusters.
		[Fact]
		public void Prefix_CombiningMark_TruncatesByCodePoint()
		{
			var vars = new Dictionary<string, object?>
			{
				// e + combining acute accent + f + g = 4 code points, 3 graphemes
				["var"] = "éfg",
			};

			// {var:1} -> first code point only -> "e"
			new UriTemplate("{var:1}").Expand(vars).Should().Be("e");

			// {var:2} -> first 2 code points -> "e" + combining acute U+0301
			// U+0301 in UTF-8 is 0xCC 0x81; both bytes are non-unreserved -> percent-encoded
			new UriTemplate("{var:2}").Expand(vars).Should().Be("e%CC%81");
		}

		// §6.1 Prefix_DoesNotSplitPctTriplet: {var:2} with var="%7Ex"
		// The prefix truncation operates on the raw string value before encoding,
		// so 2 text elements of "%7Ex" is "%7" which encodes to "%257".
		// This matches the implementation's decoded-value behavior.
		[Fact]
		public void Prefix_DoesNotSplitPctTriplet()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = "%7Ex",
			};
			var template = new UriTemplate("{var:2}");
			// Prefix 2 on raw value "%7Ex" (4 chars: %, 7, E, x) → "%7" → encoded "%257"
			template.Expand(vars).Should().Be("%257");
		}

		// ----------------------------------------------------------------
		// 6.2 List values
		// ----------------------------------------------------------------

		// §6.2 List_Simple: {list} -> red,green,blue
		[Fact]
		public void List_Simple()
		{
			var template = new UriTemplate("{list}");
			template.Expand(MakeVariables()).Should().Be("red,green,blue");
		}

		// §6.2 List_SimpleExplode: {list*} -> red,green,blue
		[Fact]
		public void List_SimpleExplode()
		{
			var template = new UriTemplate("{list*}");
			template.Expand(MakeVariables()).Should().Be("red,green,blue");
		}

		// §6.2 List_Reserved: {+list} -> red,green,blue
		[Fact]
		public void List_Reserved()
		{
			var template = new UriTemplate("{+list}");
			template.Expand(MakeVariables()).Should().Be("red,green,blue");
		}

		// §6.2 List_ReservedExplode: {+list*} -> red,green,blue
		[Fact]
		public void List_ReservedExplode()
		{
			var template = new UriTemplate("{+list*}");
			template.Expand(MakeVariables()).Should().Be("red,green,blue");
		}

		// §6.2 List_Fragment: {#list} -> #red,green,blue
		[Fact]
		public void List_Fragment()
		{
			var template = new UriTemplate("{#list}");
			template.Expand(MakeVariables()).Should().Be("#red,green,blue");
		}

		// §6.2 List_FragmentExplode: {#list*} -> #red,green,blue
		[Fact]
		public void List_FragmentExplode()
		{
			var template = new UriTemplate("{#list*}");
			template.Expand(MakeVariables()).Should().Be("#red,green,blue");
		}

		// §6.2 List_Label: X{.list} -> X.red,green,blue
		[Fact]
		public void List_Label()
		{
			var template = new UriTemplate("X{.list}");
			template.Expand(MakeVariables()).Should().Be("X.red,green,blue");
		}

		// §6.2 List_LabelExplode: X{.list*} -> X.red.green.blue
		[Fact]
		public void List_LabelExplode()
		{
			var template = new UriTemplate("X{.list*}");
			template.Expand(MakeVariables()).Should().Be("X.red.green.blue");
		}

		// §6.2 List_Path: {/list} -> /red,green,blue
		[Fact]
		public void List_Path()
		{
			var template = new UriTemplate("{/list}");
			template.Expand(MakeVariables()).Should().Be("/red,green,blue");
		}

		// §6.2 List_PathExplode: {/list*} -> /red/green/blue
		[Fact]
		public void List_PathExplode()
		{
			var template = new UriTemplate("{/list*}");
			template.Expand(MakeVariables()).Should().Be("/red/green/blue");
		}

		// §6.2 List_Semicolon: {;list} -> ;list=red,green,blue
		[Fact]
		public void List_Semicolon()
		{
			var template = new UriTemplate("{;list}");
			template.Expand(MakeVariables()).Should().Be(";list=red,green,blue");
		}

		// §6.2 List_SemicolonExplode: {;list*} -> ;list=red;list=green;list=blue
		[Fact]
		public void List_SemicolonExplode()
		{
			var template = new UriTemplate("{;list*}");
			template.Expand(MakeVariables()).Should().Be(";list=red;list=green;list=blue");
		}

		// §6.2 List_Query: {?list} -> ?list=red,green,blue
		[Fact]
		public void List_Query()
		{
			var template = new UriTemplate("{?list}");
			template.Expand(MakeVariables()).Should().Be("?list=red,green,blue");
		}

		// §6.2 List_QueryExplode: {?list*} -> ?list=red&list=green&list=blue
		[Fact]
		public void List_QueryExplode()
		{
			var template = new UriTemplate("{?list*}");
			template.Expand(MakeVariables()).Should().Be("?list=red&list=green&list=blue");
		}

		// §6.2 List_Ampersand: {&list} -> &list=red,green,blue
		[Fact]
		public void List_Ampersand()
		{
			var template = new UriTemplate("{&list}");
			template.Expand(MakeVariables()).Should().Be("&list=red,green,blue");
		}

		// §6.2 List_AmpersandExplode: {&list*} -> &list=red&list=green&list=blue
		[Fact]
		public void List_AmpersandExplode()
		{
			var template = new UriTemplate("{&list*}");
			template.Expand(MakeVariables()).Should().Be("&list=red&list=green&list=blue");
		}

		// §6.2 EmptyList_IsUndefined: {?empty_keys} -> (empty)
		// Note: empty_keys is an empty KVP list, but the "EmptyList_IsUndefined" name
		// from the test plan checks that an empty list produces no output.
		[Fact]
		public void EmptyList_IsUndefined()
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string>(),
			};
			var template = new UriTemplate("{?list}");
			template.Expand(vars).Should().Be("");
		}

		// ----------------------------------------------------------------
		// 6.3 Associative arrays (keys)
		// ----------------------------------------------------------------

		// §6.3 Keys_Simple: {keys} -> semi,%3B,dot,.,comma,%2C
		[Fact]
		public void Keys_Simple()
		{
			var template = new UriTemplate("{keys}");
			template.Expand(MakeVariables()).Should().Be("semi,%3B,dot,.,comma,%2C");
		}

		// §6.3 Keys_SimpleExplode: {keys*} -> semi=%3B,dot=.,comma=%2C
		[Fact]
		public void Keys_SimpleExplode()
		{
			var template = new UriTemplate("{keys*}");
			template.Expand(MakeVariables()).Should().Be("semi=%3B,dot=.,comma=%2C");
		}

		// §6.3 Keys_Reserved: {+keys} -> semi,;,dot,.,comma,,
		[Fact]
		public void Keys_Reserved()
		{
			var template = new UriTemplate("{+keys}");
			template.Expand(MakeVariables()).Should().Be("semi,;,dot,.,comma,,");
		}

		// §6.3 Keys_ReservedExplode: {+keys*} -> semi=;,dot=.,comma=,
		[Fact]
		public void Keys_ReservedExplode()
		{
			var template = new UriTemplate("{+keys*}");
			template.Expand(MakeVariables()).Should().Be("semi=;,dot=.,comma=,");
		}

		// §6.3 Keys_Fragment: {#keys} -> #semi,;,dot,.,comma,,
		[Fact]
		public void Keys_Fragment()
		{
			var template = new UriTemplate("{#keys}");
			template.Expand(MakeVariables()).Should().Be("#semi,;,dot,.,comma,,");
		}

		// §6.3 Keys_FragmentExplode: {#keys*} -> #semi=;,dot=.,comma=,
		[Fact]
		public void Keys_FragmentExplode()
		{
			var template = new UriTemplate("{#keys*}");
			template.Expand(MakeVariables()).Should().Be("#semi=;,dot=.,comma=,");
		}

		// §6.3 Keys_Label: X{.keys} -> X.semi,%3B,dot,.,comma,%2C
		[Fact]
		public void Keys_Label()
		{
			var template = new UriTemplate("X{.keys}");
			template.Expand(MakeVariables()).Should().Be("X.semi,%3B,dot,.,comma,%2C");
		}

		// §6.3 Keys_LabelExplode: X{.keys*} -> X.semi=%3B.dot=..comma=%2C
		[Fact]
		public void Keys_LabelExplode()
		{
			var template = new UriTemplate("X{.keys*}");
			template.Expand(MakeVariables()).Should().Be("X.semi=%3B.dot=..comma=%2C");
		}

		// §6.3 Keys_Path: {/keys} -> /semi,%3B,dot,.,comma,%2C
		[Fact]
		public void Keys_Path()
		{
			var template = new UriTemplate("{/keys}");
			template.Expand(MakeVariables()).Should().Be("/semi,%3B,dot,.,comma,%2C");
		}

		// §6.3 Keys_PathExplode: {/keys*} -> /semi=%3B/dot=./comma=%2C
		[Fact]
		public void Keys_PathExplode()
		{
			var template = new UriTemplate("{/keys*}");
			template.Expand(MakeVariables()).Should().Be("/semi=%3B/dot=./comma=%2C");
		}

		// §6.3 Keys_Semicolon: {;keys} -> ;keys=semi,%3B,dot,.,comma,%2C
		[Fact]
		public void Keys_Semicolon()
		{
			var template = new UriTemplate("{;keys}");
			template.Expand(MakeVariables()).Should().Be(";keys=semi,%3B,dot,.,comma,%2C");
		}

		// §6.3 Keys_SemicolonExplode: {;keys*} -> ;semi=%3B;dot=.;comma=%2C
		[Fact]
		public void Keys_SemicolonExplode()
		{
			var template = new UriTemplate("{;keys*}");
			template.Expand(MakeVariables()).Should().Be(";semi=%3B;dot=.;comma=%2C");
		}

		// §6.3 Keys_Query: {?keys} -> ?keys=semi,%3B,dot,.,comma,%2C
		[Fact]
		public void Keys_Query()
		{
			var template = new UriTemplate("{?keys}");
			template.Expand(MakeVariables()).Should().Be("?keys=semi,%3B,dot,.,comma,%2C");
		}

		// §6.3 Keys_QueryExplode: {?keys*} -> ?semi=%3B&dot=.&comma=%2C
		[Fact]
		public void Keys_QueryExplode()
		{
			var template = new UriTemplate("{?keys*}");
			template.Expand(MakeVariables()).Should().Be("?semi=%3B&dot=.&comma=%2C");
		}

		// §6.3 Keys_Ampersand: {&keys} -> &keys=semi,%3B,dot,.,comma,%2C
		[Fact]
		public void Keys_Ampersand()
		{
			var template = new UriTemplate("{&keys}");
			template.Expand(MakeVariables()).Should().Be("&keys=semi,%3B,dot,.,comma,%2C");
		}

		// §6.3 Keys_AmpersandExplode: {&keys*} -> &semi=%3B&dot=.&comma=%2C
		[Fact]
		public void Keys_AmpersandExplode()
		{
			var template = new UriTemplate("{&keys*}");
			template.Expand(MakeVariables()).Should().Be("&semi=%3B&dot=.&comma=%2C");
		}

		// §6.3 EmptyKeys_IsUndefined: X{.empty_keys} -> X
		[Fact]
		public void EmptyKeys_IsUndefined()
		{
			var template = new UriTemplate("X{.empty_keys}");
			template.Expand(MakeVariables()).Should().Be("X");
		}

		// §6.3 EmptyKeysExploded_IsUndefined: X{.empty_keys*} -> X
		[Fact]
		public void EmptyKeysExploded_IsUndefined()
		{
			var template = new UriTemplate("X{.empty_keys*}");
			template.Expand(MakeVariables()).Should().Be("X");
		}

		// ----------------------------------------------------------------
		// Edge cases (beyond test-plan.md §6)
		// ----------------------------------------------------------------

		// Prefix + explode mutual exclusion at parse time

		[Theory]
		[InlineData("{var:3*}")]
		[InlineData("{var*:3}")]
		public void PrefixAndExplode_ThrowsFormatException(string template)
		{
			Action act = () => new UriTemplate(template);
			act.Should().Throw<FormatException>();
		}

		// Prefix on a list value at expansion time

		[Fact]
		public void PrefixOnListValue_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string> { "red", "green", "blue" },
			};
			var template = new UriTemplate("{list:3}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("list");
		}

		// Prefix on an associative-array value at expansion time

		[Fact]
		public void PrefixOnAssociativeArrayValue_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("semi", ";"),
				},
			};
			var template = new UriTemplate("{keys:3}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("keys");
		}

		// Empty list [] is undefined -> produces no output across operators

		[Theory]
		[InlineData("{list}", "")]
		[InlineData("{?list}", "")]
		[InlineData("{/list*}", "")]
		[InlineData("{;list*}", "")]
		[InlineData("{#list}", "")]
		[InlineData("{+list*}", "")]
		public void EmptyList_ProducesNoOutput(string templateStr, string expected)
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string>(),
			};
			var template = new UriTemplate(templateStr);
			template.Expand(vars).Should().Be(expected);
		}

		// Empty associative array {} is undefined -> produces no output

		[Theory]
		[InlineData("{keys}", "")]
		[InlineData("{?keys}", "")]
		[InlineData("{/keys*}", "")]
		[InlineData("{;keys}", "")]
		[InlineData("{#keys*}", "")]
		public void EmptyAssociativeArray_ProducesNoOutput(string templateStr, string expected)
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>(),
			};
			var template = new UriTemplate(templateStr);
			template.Expand(vars).Should().Be(expected);
		}

		// List with single member

		[Theory]
		[InlineData("{list}", "red")]
		[InlineData("{list*}", "red")]
		[InlineData("{?list}", "?list=red")]
		[InlineData("{?list*}", "?list=red")]
		[InlineData("{/list}", "/red")]
		[InlineData("{/list*}", "/red")]
		[InlineData("{;list}", ";list=red")]
		[InlineData("{;list*}", ";list=red")]
		public void SingleMemberList_ExpandsCorrectly(string templateStr, string expected)
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string> { "red" },
			};
			var template = new UriTemplate(templateStr);
			template.Expand(vars).Should().Be(expected);
		}

		// Associative array with single pair

		[Fact]
		public void SinglePairAssociativeArray_NoExplode()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("semi", ";"),
				},
			};
			var template = new UriTemplate("{keys}");
			template.Expand(vars).Should().Be("semi,%3B");
		}

		[Fact]
		public void SinglePairAssociativeArray_Explode()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("semi", ";"),
				},
			};
			var template = new UriTemplate("{?keys*}");
			template.Expand(vars).Should().Be("?semi=%3B");
		}

		// List containing empty-string members

		[Fact]
		public void ListWithEmptyStringMembers_QueryExplode()
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string> { "", "green", "" },
			};
			var template = new UriTemplate("{?list*}");
			template.Expand(vars).Should().Be("?list=&list=green&list=");
		}

		[Fact]
		public void ListWithEmptyStringMembers_SemicolonExplode()
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string> { "", "green", "" },
			};
			var template = new UriTemplate("{;list*}");
			// Semicolon ifEmp: no = for empty members
			template.Expand(vars).Should().Be(";list;list=green;list");
		}

		// Prefix length larger than string length -> returns full value

		[Fact]
		public void PrefixLargerThanLength_ReturnsFullValue()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = "value",
			};
			var template = new UriTemplate("{var:9999}");
			template.Expand(vars).Should().Be("value");
		}

		// Prefix length 1 on emoji/multi-byte string

		[Fact]
		public void PrefixOnEmoji_ReturnsSingleTextElement()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = "\U0001F600x",
			};
			var template = new UriTemplate("{var:1}");
			// Single text element is the emoji codepoint, encoded as UTF-8
			template.Expand(vars).Should().Be("%F0%9F%98%80");
		}

		// Combined Level 1-3 + Level 4 expressions in one template

		[Fact]
		public void MixedLevel1Through4_InOneTemplate()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = "value",
				["hello"] = "Hello World!",
				["path"] = "/foo/bar",
				["list"] = new List<string> { "red", "green", "blue" },
				["x"] = "1024",
			};
			var template = new UriTemplate("{var}{+path}{?list*}{&x}");
			template.Expand(vars).Should().Be("value/foo/bar?list=red&list=green&list=blue&x=1024");
		}

		// Unsupported value type -> FormatException naming the variable

		[Fact]
		public void UnsupportedValueType_Int_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = 42,
			};
			var template = new UriTemplate("{var}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("var");
		}

		[Fact]
		public void UnsupportedValueType_Bool_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = true,
			};
			var template = new UriTemplate("{var}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("var");
		}

		[Fact]
		public void UnsupportedValueType_Object_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = new object(),
			};
			var template = new UriTemplate("{var}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("var");
		}

		// null value behaves as undefined with IDictionary<string, object?> overload

		[Fact]
		public void NullValueInObjectDictionary_TreatedAsUndefined()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = null,
			};
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("");
		}

		[Fact]
		public void NullValueInObjectDictionary_WrappedByLiterals()
		{
			var vars = new Dictionary<string, object?>
			{
				["var"] = null,
			};
			var template = new UriTemplate("O{var}X");
			template.Expand(vars).Should().Be("OX");
		}

		// List with null element -> FormatException

		[Fact]
		public void ListWithNullElement_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["list"] = new List<string> { "red", null!, "blue" },
			};
			var template = new UriTemplate("{list}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("list");
		}

		// Associative array with null value -> FormatException

		[Fact]
		public void AssociativeArrayWithNullValue_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("semi", ";"),
					new("dot", null!),
				},
			};
			var template = new UriTemplate("{keys}");
			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("keys");
		}

		// D4: IEnumerable<KeyValuePair<string,string>> dispatch path via List<KVP>

		[Fact]
		public void ListOfKeyValuePairs_DispatchesAsAssociativeArray()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("a", "1"),
					new("b", "2"),
				},
			};
			var template = new UriTemplate("{?keys*}");
			template.Expand(vars).Should().Be("?a=1&b=2");
		}

		// D5: IDictionary<string,string> dispatch path via Dictionary<string,string>
		// Dictionary order is non-deterministic; accept all orderings for 2-key dict.

		[Fact]
		public void DictionaryOfStringString_DispatchesAsAssociativeArray()
		{
			var dict = new Dictionary<string, string>
			{
				["a"] = "1",
				["b"] = "2",
			};
			var vars = new Dictionary<string, object?>
			{
				["keys"] = dict,
			};
			var template = new UriTemplate("{?keys*}");
			template.Expand(vars).Should().BeOneOf("?a=1&b=2", "?b=2&a=1");
		}

		// D6: Existing Expand(IDictionary<string,string>) overload still works for Level 4

		[Fact]
		public void StringDictionaryOverload_WorksWithPrefixModifier()
		{
			var vars = new Dictionary<string, string>
			{
				["var"] = "value",
			};
			var template = new UriTemplate("{var:3}");
			template.Expand(vars).Should().Be("val");
		}

		[Fact]
		public void StringDictionaryOverload_WorksWithExplodeModifier()
		{
			// When using IDictionary<string,string>, values are strings.
			// {list*} on a string value treats it as a single-item string.
			// Per the type dispatch: string is matched first before IEnumerable<string>.
			var vars = new Dictionary<string, string>
			{
				["list"] = "red",
			};
			var template = new UriTemplate("{list*}");
			// String with explode: explode has no effect on a string value;
			// it expands as a normal string.
			template.Expand(vars).Should().Be("red");
		}
	}
}
