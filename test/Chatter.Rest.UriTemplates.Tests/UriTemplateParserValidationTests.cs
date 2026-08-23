using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Input-validation contract for <see cref="UriTemplateParser"/> and
	/// <see cref="UriTemplateEncoder"/>: invalid input is rejected, never silently
	/// accepted or repaired. See RFC 6570 section 2.1 (literals) and section 2.3 (varname).
	/// </summary>
	public class UriTemplateParserValidationTests
	{
		private static readonly Dictionary<string, string> Variables = new()
		{
			["var"] = "value",
		};

		// --- Finding 5: Parse(null) argument contract ---

		[Fact]
		public void Parse_NullTemplate_ThrowsArgumentNullException()
		{
			IUriTemplateParser parser = UriTemplateParser.Default;

			Action act = () => parser.Parse(null!);

			act.Should().Throw<ArgumentNullException>()
				.And.ParamName.Should().Be("template");
		}

		// --- Finding 1: leading dot in a variable name ---

		[Fact]
		public void VarName_LeadingDot_Rejected()
		{
			Action act = () => new UriTemplate("{x,.y}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Leading dot");
		}

		[Fact]
		public void VarName_LeadingDotAfterOperator_Rejected()
		{
			Action act = () => new UriTemplate("{+x,.y}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Leading dot");
		}

		[Fact]
		public void VarName_LeadingDotWithExplodeModifier_Rejected()
		{
			Action act = () => new UriTemplate("{x,.z*}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Leading dot");
		}

		[Fact]
		public void VarName_InteriorDot_StillAccepted()
		{
			var tokens = UriTemplateParser.Default.Parse("{a.b}");

			var expression = tokens.Should().ContainSingle()
				.Which.Should().BeOfType<UriTemplateExpressionToken>().Subject;
			expression.Variables.Should().ContainSingle()
				.Which.Name.Should().Be("a.b");
		}

		// --- Finding 2: literal characters outside the RFC 6570 section 2.1 literals set ---

		/// <summary>
		/// Independent oracle for the ASCII part of the RFC 6570 section 2.1 literals production:
		/// %x21 / %x23-24 / %x26 / %x28-3B / %x3D / %x3F-5B / %x5D / %x5F / %x61-7A / %x7E.
		/// </summary>
		private static bool IsRfcLiteralAscii(int code)
		{
			return code == 0x21 ||
				(code >= 0x23 && code <= 0x24) ||
				code == 0x26 ||
				(code >= 0x28 && code <= 0x3B) ||
				code == 0x3D ||
				(code >= 0x3F && code <= 0x5B) ||
				code == 0x5D ||
				code == 0x5F ||
				(code >= 0x61 && code <= 0x7A) ||
				code == 0x7E;
		}

		[Fact]
		public void Literal_EveryAsciiCharacter_MatchesRfc6570LiteralsSet()
		{
			// 0x25 '%' starts a pct-encoded triplet and 0x7B '{' opens an expression;
			// both have their own dedicated parser branches and tests.
			for (var code = 0; code <= 0x7F; code++)
			{
				if (code == 0x25 || code == 0x7B)
				{
					continue;
				}

				var c = (char)code;
				Action act = () => new UriTemplate("/a" + c + "b/{var}");

				if (IsRfcLiteralAscii(code))
				{
					act.Should().NotThrow($"0x{code:X2} is in the RFC 6570 literals set");
				}
				else
				{
					act.Should().Throw<FormatException>($"0x{code:X2} is outside the RFC 6570 literals set")
						.And.Message.Should().Contain("Invalid literal character");
				}
			}
		}

		[Fact]
		public void Literal_Tab_Rejected()
		{
			Action act = () => new UriTemplate("/a\tb/{var}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Invalid literal character");
		}

		[Fact]
		public void Literal_TrailingSegmentIsValidatedToo()
		{
			// The final literal run (no further '{') goes through the same validation path.
			Action act = () => new UriTemplate("a\r\nb");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Invalid literal character");
		}

		[Fact]
		public void Literal_PipeAndAngleBrackets_Rejected()
		{
			Action act = () => new UriTemplate("/a|b<c>/{var}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Invalid literal character");
		}

		[Fact]
		public void Literal_ControlCharacterMessageUsesUnicodeEscape()
		{
			Action act = () => new UriTemplate("/a\tb");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("\\u0009");
		}

		[Fact]
		public void Literal_SpaceMessageStillNamesTheSpace()
		{
			Action act = () => new UriTemplate("/bad literal/{var}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Invalid literal character ' '");
		}

		[Fact]
		public void Literal_ValidPercentTriplet_StillPassesThrough()
		{
			var template = new UriTemplate("/a%2Fb/{var}");

			template.Expand(Variables).Should().Be("/a%2Fb/value");
		}

		[Fact]
		public void Literal_BarePercent_StillRejected()
		{
			Action act = () => new UriTemplate("/a%zz/{var}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("percent-encoded triplet");
		}

		// --- Finding 3: unpaired surrogates in a literal segment ---

		[Fact]
		public void Literal_UnpairedHighSurrogate_Rejected()
		{
			// Previously produced "a%EF%BF%BD%62": the surrogate became U+FFFD and the
			// valid 'b' was consumed into the same UTF-8 encode and emitted as %62.
			Action act = () => new UriTemplate("a\uD800b");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Unpaired high surrogate");
		}

		[Fact]
		public void Literal_TrailingHighSurrogate_Rejected()
		{
			Action act = () => new UriTemplate("a\uD800");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Unpaired high surrogate");
		}

		[Fact]
		public void Literal_LoneLowSurrogate_Rejected()
		{
			Action act = () => new UriTemplate("a\uDC00b");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("Unpaired low surrogate");
		}

		[Fact]
		public void Literal_ValidSurrogatePair_EncodedAsUtf8()
		{
			// U+1F600 GRINNING FACE
			var template = new UriTemplate("/\U0001F600/{var}");

			template.Expand(Variables).Should().Be("/%F0%9F%98%80/value");
		}

		[Fact]
		public void Literal_NonAsciiBmp_StillEncodedAsUtf8()
		{
			var template = new UriTemplate("/café/{var}");

			template.Expand(Variables).Should().Be("/caf%C3%A9/value");
		}

		// --- Finding 4: unpaired surrogates in a variable value ---

		[Fact]
		public void EncodeUnreserved_UnpairedSurrogate_ThrowsFormatException()
		{
			Action act = () => UriTemplateEncoder.EncodeUnreserved("a\uD83Db");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void EncodeReserved_UnpairedSurrogate_ThrowsFormatException()
		{
			Action act = () => UriTemplateEncoder.EncodeReserved("a\uD83Db");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void Expand_UnpairedSurrogateValue_ThrowsInsteadOfMangling()
		{
			var template = new UriTemplate("{v}");
			var variables = new Dictionary<string, string> { ["v"] = "a\uD83Db" };

			Action act = () => template.Expand(variables);

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void Expand_UnpairedSurrogateValueWithPrefix_ThrowsInsteadOfMangling()
		{
			// Previously expanded to "a%EF%BF%BD": the lone surrogate silently became U+FFFD
			// and consumed one code point of the two-character prefix budget, losing the 'b'.
			var template = new UriTemplate("{v:2}");
			var variables = new Dictionary<string, string> { ["v"] = "a\uD83Db" };

			Action act = () => template.Expand(variables);

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void Expand_UnpairedSurrogateValueReservedOperator_ThrowsInsteadOfMangling()
		{
			var template = new UriTemplate("{+v}");
			var variables = new Dictionary<string, string> { ["v"] = "a\uD83Db" };

			Action act = () => template.Expand(variables);

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void Expand_ValidSurrogatePairValue_StillEncodedAsUtf8()
		{
			var template = new UriTemplate("{v}");
			var variables = new Dictionary<string, string> { ["v"] = "a\U0001F600b" };

			template.Expand(variables).Should().Be("a%F0%9F%98%80b");
		}
	}
}
