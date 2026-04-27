using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	public class UriTemplateEdgeCaseTests
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

		// 5.1 Template parsing edge cases

		[Fact]
		public void EmptyTemplate()
		{
			var template = new UriTemplate("");
			template.Expand(Variables).Should().Be("");
		}

		[Fact]
		public void LiteralOnly()
		{
			var template = new UriTemplate("/orders/list");
			template.Expand(Variables).Should().Be("/orders/list");
		}

		[Fact]
		public void ExpressionAtStart()
		{
			var template = new UriTemplate("{var}/rest");
			template.Expand(Variables).Should().Be("value/rest");
		}

		[Fact]
		public void ExpressionAtEnd()
		{
			var template = new UriTemplate("/prefix/{var}");
			template.Expand(Variables).Should().Be("/prefix/value");
		}

		[Fact]
		public void ExpressionOnly()
		{
			var template = new UriTemplate("{var}");
			template.Expand(Variables).Should().Be("value");
		}

		[Fact]
		public void ConsecutiveExpressions()
		{
			var template = new UriTemplate("{x}{y}");
			template.Expand(Variables).Should().Be("1024768");
		}

		[Fact]
		public void MultipleExpressionsWithLiterals()
		{
			var template = new UriTemplate("/a/{x}/b/{y}/c");
			template.Expand(Variables).Should().Be("/a/1024/b/768/c");
		}

		// 5.2 Mixed-level expressions in one template

		[Fact]
		public void Level1AndLevel3Query()
		{
			var vars = new Dictionary<string, string>
			{
				["id"] = "42",
				["status"] = "open",
				["page"] = "2",
			};
			var template = new UriTemplate("/orders/{id}{?status,page}");
			template.Expand(vars).Should().Be("/orders/42?status=open&page=2");
		}

		[Fact]
		public void Level1AndLevel2Reserved()
		{
			var vars = new Dictionary<string, string>
			{
				["path"] = "foo/bar",
			};
			var template = new UriTemplate("/proxy/{+path}/tail");
			template.Expand(vars).Should().Be("/proxy/foo/bar/tail");
		}

		[Fact]
		public void Level2AndLevel3()
		{
			var vars = new Dictionary<string, string>
			{
				["base"] = "/root",
				["segment"] = "value",
			};
			var template = new UriTemplate("{+base}{/segment}");
			template.Expand(vars).Should().Be("/root/value");
		}

		[Fact]
		public void MixedReservedAndSimpleEncoding()
		{
			var template = new UriTemplate("{+base}{var}{?half}");
			template.Expand(Variables).Should().Be("http://example.com/home/value?half=50%25");
		}

		// 5.3 Malformed template handling

		[Fact]
		public void UnclosedBrace_Throws()
		{
			Action act = () => new UriTemplate("/orders/{id");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void NestedBraces_Throws()
		{
			Action act = () => new UriTemplate("/orders/{{id}}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void EmptyExpression_Throws()
		{
			Action act = () => new UriTemplate("/orders/{}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void OperatorOnlyQuery_Throws()
		{
			Action act = () => new UriTemplate("{?}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void TrailingComma_Throws()
		{
			Action act = () => new UriTemplate("{x,}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void LeadingComma_Throws()
		{
			Action act = () => new UriTemplate("{,x}");
			act.Should().Throw<NotSupportedException>();
		}

		[Fact]
		public void DoubleComma_Throws()
		{
			Action act = () => new UriTemplate("{x,,y}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void WhitespaceInExpression_Throws()
		{
			Action act = () => new UriTemplate("{ x }");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void WhitespaceAfterComma_Throws()
		{
			Action act = () => new UriTemplate("{x, y}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidVarNameHyphen_Throws()
		{
			Action act = () => new UriTemplate("{bad-name}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidVarNameDollar_Throws()
		{
			Action act = () => new UriTemplate("{bad$name}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidVarNameSlash_Throws()
		{
			Action act = () => new UriTemplate("{bad/name}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidVarNameConsecutiveDots_Throws()
		{
			Action act = () => new UriTemplate("{a..b}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidVarNameTrailingDot_Throws()
		{
			Action act = () => new UriTemplate("{a.}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidVarNameLeadingDotNoOperator_Throws()
		{
			Action act = () => new UriTemplate("{.}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void InvalidPctEncodedVarName_Throws()
		{
			Action act = () => new UriTemplate("{%zz}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void DoubleOperator_Throws()
		{
			Action act = () => new UriTemplate("{??x}");
			act.Should().Throw<FormatException>();
		}

		// 5.4 Reserved future operators

		[Fact]
		public void ReservedEqualsOperator_Throws()
		{
			Action act = () => new UriTemplate("{=var}");
			act.Should().Throw<NotSupportedException>();
		}

		[Fact]
		public void ReservedCommaOperator_Throws()
		{
			Action act = () => new UriTemplate("{,var}");
			act.Should().Throw<NotSupportedException>();
		}

		[Fact]
		public void ReservedBangOperator_Throws()
		{
			Action act = () => new UriTemplate("{!var}");
			act.Should().Throw<NotSupportedException>();
		}

		[Fact]
		public void ReservedAtOperator_Throws()
		{
			Action act = () => new UriTemplate("{@var}");
			act.Should().Throw<NotSupportedException>();
		}

		[Fact]
		public void ReservedPipeOperator_Throws()
		{
			Action act = () => new UriTemplate("{|var}");
			act.Should().Throw<NotSupportedException>();
		}

		// 5.5 Level 4 modifier validation
		// Note: PrefixModifier_Throws, ExplodeModifier_Throws,
		// ExplodeWithOperator_Throws, and PrefixModifierMaxLengthFourDigits_Throws
		// were removed because Level 4 is now supported. Coverage moved to
		// UriTemplateLevel4Tests. The remaining tests verify malformed modifier syntax.

		[Theory]
		[InlineData("{var:3*}")]
		[InlineData("{var*:3}")]
		public void PrefixAndExplodeBoth_ThrowsFormat(string template)
		{
			Action act = () => new UriTemplate(template);
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void PrefixModifierZero_ThrowsFormat()
		{
			Action act = () => new UriTemplate("{var:0}");
			act.Should().Throw<FormatException>();
		}

		[Theory]
		[InlineData("{var:01}")]
		[InlineData("{var:001}")]
		[InlineData("{var:0001}")]
		[InlineData("{var:0123}")]
		[InlineData("{var:00}")]
		public void PrefixModifierLeadingZero_ThrowsFormat(string template)
		{
			Action act = () => new UriTemplate(template);
			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("leading zeros");
		}

		[Fact]
		public void PrefixModifierTooLarge_ThrowsFormat()
		{
			Action act = () => new UriTemplate("{var:10000}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void PrefixModifierNonNumeric_ThrowsFormat()
		{
			Action act = () => new UriTemplate("{var:abc}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void ModifierInMiddle_ThrowsFormat()
		{
			Action act = () => new UriTemplate("{va*r}");
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void ColonWithoutLength_ThrowsFormat()
		{
			Action act = () => new UriTemplate("{var:}");
			act.Should().Throw<FormatException>();
		}

		// 5.6 Case sensitivity

		[Fact]
		public void VariableNames_CaseSensitive()
		{
			var vars = new Dictionary<string, string>
			{
				["Var"] = "upper",
				["var"] = "lower",
			};
			var template = new UriTemplate("{Var}");
			template.Expand(vars).Should().Be("upper");
		}

		[Fact]
		public void CaseInsensitiveDictionaryDoesNotChangeTemplateSemantics()
		{
			var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["var"] = "lower",
			};
			var template = new UriTemplate("{Var}");
			template.Expand(vars).Should().Be("");
		}

		[Fact]
		public void OrdinalDuplicateNamesRemainDistinct()
		{
			var vars = new Dictionary<string, string>
			{
				["var"] = "lower",
				["Var"] = "upper",
			};
			var template = new UriTemplate("{var,Var}");
			template.Expand(vars).Should().Be("lower,upper");
		}
	}
}
