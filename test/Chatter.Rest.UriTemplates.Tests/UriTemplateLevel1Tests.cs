using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	public class UriTemplateLevel1Tests
	{
		private static readonly Dictionary<string, string> Variables = new()
		{
			["var"] = "value",
			["hello"] = "Hello World!",
			["half"] = "50%",
			["who"] = "fred",
			["base"] = "http://example.com/home/",
			["dub"] = "me/too",
			["v"] = "6",
			["empty"] = "",
			["path"] = "/foo/bar",
			["x"] = "1024",
			["y"] = "768",
		};
		// "undef" is intentionally absent

		// 1.1 RFC canonical examples

		[Fact]
		public void SingleVar_SimpleValue()
		{
			var template = new UriTemplate("{var}");
			template.Expand(Variables).Should().Be("value");
		}

		[Fact]
		public void SingleVar_WithSpaceAndBang()
		{
			var template = new UriTemplate("{hello}");
			template.Expand(Variables).Should().Be("Hello%20World%21");
		}

		[Fact]
		public void SingleVar_PercentEncoded()
		{
			var template = new UriTemplate("{half}");
			template.Expand(Variables).Should().Be("50%25");
		}

		[Fact]
		public void SingleVar_EmptyValue()
		{
			var template = new UriTemplate("{empty}");
			template.Expand(Variables).Should().Be("");
		}

		[Fact]
		public void SingleVar_Undefined()
		{
			var template = new UriTemplate("{undef}");
			template.Expand(Variables).Should().Be("");
		}

		[Fact]
		public void SingleVar_EmptyValueWrappedByLiterals()
		{
			var template = new UriTemplate("O{empty}X");
			template.Expand(Variables).Should().Be("OX");
		}

		[Fact]
		public void SingleVar_UndefinedWrappedByLiterals()
		{
			var template = new UriTemplate("O{undef}X");
			template.Expand(Variables).Should().Be("OX");
		}

		[Fact]
		public void SingleVar_WithSlashes()
		{
			var template = new UriTemplate("{path}");
			template.Expand(Variables).Should().Be("%2Ffoo%2Fbar");
		}

		[Fact]
		public void NoOp_TwoVars()
		{
			var template = new UriTemplate("{x,y}");
			template.Expand(Variables).Should().Be("1024,768");
		}

		[Fact]
		public void NoOp_ThreeVars()
		{
			var template = new UriTemplate("{x,hello,y}");
			template.Expand(Variables).Should().Be("1024,Hello%20World%21,768");
		}

		[Fact]
		public void MultipleVars_WithEmpty()
		{
			var template = new UriTemplate("?{x,empty}");
			template.Expand(Variables).Should().Be("?1024,");
		}

		[Fact]
		public void MultipleVars_WithUndefinedTail()
		{
			var template = new UriTemplate("?{x,undef}");
			template.Expand(Variables).Should().Be("?1024");
		}

		[Fact]
		public void MultipleVars_WithUndefinedHead()
		{
			var template = new UriTemplate("?{undef,y}");
			template.Expand(Variables).Should().Be("?768");
		}

		// 1.2 Literal text preservation

		[Fact]
		public void NoExpression_LiteralOnly()
		{
			var template = new UriTemplate("/orders/list");
			template.Expand(Variables).Should().Be("/orders/list");
		}

		[Fact]
		public void LeadingLiteral()
		{
			var template = new UriTemplate("/orders/{var}");
			template.Expand(Variables).Should().Be("/orders/value");
		}

		[Fact]
		public void TrailingLiteral()
		{
			var template = new UriTemplate("{var}/orders");
			template.Expand(Variables).Should().Be("value/orders");
		}

		[Fact]
		public void MiddleLiteral()
		{
			var template = new UriTemplate("/orders/{var}/items");
			template.Expand(Variables).Should().Be("/orders/value/items");
		}

		[Fact]
		public void EmptyTemplate()
		{
			var template = new UriTemplate("");
			template.Expand(Variables).Should().Be("");
		}

		[Fact]
		public void Literal_PctTripletPreserved()
		{
			var template = new UriTemplate("/already/%7Eencoded");
			template.Expand(Variables).Should().Be("/already/%7Eencoded");
		}

		// 1.3 Multiple expressions

		[Fact]
		public void TwoExpressions()
		{
			var template = new UriTemplate("/orders/{x}/items/{y}");
			template.Expand(Variables).Should().Be("/orders/1024/items/768");
		}

		[Fact]
		public void ConsecutiveExpressions()
		{
			var template = new UriTemplate("{x}{y}");
			template.Expand(Variables).Should().Be("1024768");
		}

		[Fact]
		public void RepeatedVariable_StaticValue()
		{
			var template = new UriTemplate("{var}/{var}");
			template.Expand(Variables).Should().Be("value/value");
		}

		[Fact]
		public void SameVariableDifferentOperators()
		{
			var template = new UriTemplate("{path}{+path}{/path}");
			template.Expand(Variables).Should().Be("%2Ffoo%2Fbar/foo/bar/%2Ffoo%2Fbar");
		}

		// 1.4 Encoding edge cases

		[Fact]
		public void Encoding_SpaceEncoded()
		{
			var template = new UriTemplate("{hello}");
			template.Expand(Variables).Should().Be("Hello%20World%21");
		}

		[Fact]
		public void Encoding_SlashEncoded()
		{
			var template = new UriTemplate("{path}");
			template.Expand(Variables).Should().Be("%2Ffoo%2Fbar");
		}

		[Fact]
		public void Encoding_TildeNotEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "val~ue" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("val~ue");
		}

		[Fact]
		public void Encoding_HyphenNotEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "val-ue" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("val-ue");
		}

		[Fact]
		public void Encoding_DotNotEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "val.ue" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("val.ue");
		}

		[Fact]
		public void Encoding_UnderscoreNotEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "val_ue" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("val_ue");
		}

		[Fact]
		public void Encoding_ReservedColonEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "val:ue" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("val%3Aue");
		}

		[Fact]
		public void Encoding_ReservedAmpersandEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "a&b" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("a%26b");
		}

		[Fact]
		public void Encoding_PercentEncoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "50%" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("50%25");
		}

		[Fact]
		public void Encoding_UnicodeUtf8Encoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "café" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("caf%C3%A9");
		}

		[Fact]
		public void Encoding_EmojiUtf8Encoded()
		{
			var vars = new Dictionary<string, string> { ["var"] = "\U0001F600" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("%F0%9F%98%80");
		}

		[Fact]
		public void Encoding_ExistingPctTripletEncodedForSimple()
		{
			var vars = new Dictionary<string, string> { ["var"] = "%7E" };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("%257E");
		}

		// 1.5 Guard conditions

		[Fact]
		public void NullDictionary_Throws()
		{
			var template = new UriTemplate("{var}");
			Action act = () => template.Expand((IDictionary<string, string>)null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void EmptyDictionary_AllExpressionsEmpty()
		{
			var template = new UriTemplate("{var}");
			template.Expand(new Dictionary<string, string>()).Should().Be("");
		}

		[Fact]
		public void TupleOverload_NullArrayThrows()
		{
			var template = new UriTemplate("{var}");
			Action act = () => template.Expand(((string Key, string Value)[])null!);
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void NullDictionaryValue_TreatedAsUndefined()
		{
			var vars = new Dictionary<string, string> { ["var"] = null! };
			var template = new UriTemplate("{var}");
			template.Expand(vars).Should().Be("");
		}

		[Fact]
		public void TupleOverload_DuplicateKeys_FirstWins()
		{
			var template = new UriTemplate("{var}");
			template.Expand(("var", "first"), ("var", "second")).Should().Be("first");
		}
	}
}
