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
		/// Independent oracle for the ASCII characters accepted in literal text: the RFC 6570
		/// section 2.1 literals production
		/// (%x21 / %x23-24 / %x26 / %x28-3B / %x3D / %x3F-5B / %x5D / %x5F / %x61-7A / %x7E),
		/// plus the apostrophe %x27.
		/// </summary>
		/// <remarks>
		/// The section 2.1 ABNF comment excludes "'", but that contradicts section 2.1's own prose
		/// (characters "allowed in a URI (reserved / unreserved / pct-encoded)" are copied through)
		/// and RFC 3986 section 2.2, which lists ' in sub-delims. The official uritemplate-test
		/// Level 1 example '{var}' -&gt; 'value' requires it to pass through. Every other character
		/// the ABNF excludes was audited against RFC 3986 and is genuinely forbidden in a URI, so
		/// this oracle still rejects all of them.
		/// </remarks>
		private static bool IsAcceptedLiteralAscii(int code)
		{
			return code == 0x21 ||
				(code >= 0x23 && code <= 0x24) ||
				code == 0x26 ||
				code == 0x27 ||
				(code >= 0x28 && code <= 0x3B) ||
				code == 0x3D ||
				(code >= 0x3F && code <= 0x5B) ||
				code == 0x5D ||
				code == 0x5F ||
				(code >= 0x61 && code <= 0x7A) ||
				code == 0x7E;
		}

		[Fact]
		public void Literal_EveryAsciiCharacter_MatchesAcceptedLiteralSet()
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

				if (IsAcceptedLiteralAscii(code))
				{
					act.Should().NotThrow($"0x{code:X2} is accepted in literal text");
				}
				else
				{
					act.Should().Throw<FormatException>($"0x{code:X2} is not accepted in literal text")
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

		// --- Codex follow-up: validate the whole value before applying a prefix ---
		//
		// The strict encoder only ever sees the value AFTER the expander has applied the
		// RFC 6570 section 2.4.1 prefix modifier, so an unpaired surrogate that sits beyond the
		// prefix boundary is truncated away before it can be rejected. UriTemplateEncoder
		// therefore exposes ValidateEncodable, which is position-independent and is meant to be
		// called on the original value before truncation.

		[Fact]
		public void ValidateEncodable_NullValue_ThrowsArgumentNullException()
		{
			Action act = () => UriTemplateEncoder.ValidateEncodable(null!);

			act.Should().Throw<ArgumentNullException>()
				.And.ParamName.Should().Be("value");
		}

		[Fact]
		public void ValidateEncodable_UnpairedHighSurrogate_ThrowsFormatException()
		{
			// The same value the {v:1} case truncates down to a bare "a".
			Action act = () => UriTemplateEncoder.ValidateEncodable("a\uD83Db");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate at index 1");
		}

		[Fact]
		public void ValidateEncodable_TrailingHighSurrogate_ThrowsFormatException()
		{
			Action act = () => UriTemplateEncoder.ValidateEncodable("ab\uD83D");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate at index 2");
		}

		[Fact]
		public void ValidateEncodable_UnpairedLowSurrogate_ThrowsFormatException()
		{
			Action act = () => UriTemplateEncoder.ValidateEncodable("ab\uDE00");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate at index 2");
		}

		[Theory]
		[InlineData("")]
		[InlineData("plain")]
		[InlineData("a\U0001F600b")]
		[InlineData("\U0001F600\U0001F601")]
		public void ValidateEncodable_WellFormedValue_DoesNotThrow(string value)
		{
			Action act = () => UriTemplateEncoder.ValidateEncodable(value);

			act.Should().NotThrow();
		}

		// The prefix modifier truncates before the encoder runs, so an unpaired surrogate beyond
		// the prefix boundary used to be discarded by TruncateByCodePoints and the malformed value
		// expanded successfully. UriTemplateExpander.ExpandString now validates the whole value
		// before truncating it.
		[Fact]
		public void Expand_UnpairedSurrogateBeyondPrefixWindow_ThrowsInsteadOfTruncatingItAway()
		{
			var template = new UriTemplate("{v:1}");
			var variables = new Dictionary<string, string> { ["v"] = "a\uD83Db" };

			Action act = () => template.Expand(variables);

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void Expand_UnpairedLowSurrogateBeyondPrefixWindow_ThrowsInsteadOfTruncatingItAway()
		{
			// The lone low surrogate sits at index 1, outside the single-code-point prefix window.
			var template = new UriTemplate("{v:1}");
			var variables = new Dictionary<string, string> { ["v"] = "a\uDC00b" };

			Action act = () => template.Expand(variables);

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("unpaired UTF-16 surrogate");
		}

		[Fact]
		public void Expand_ValidAstralPlaneValueWithPrefix_TruncatesByCodePointAndIsNotRejected()
		{
			// Two code points: 'a' and U+1F600. The surrogate pair must survive truncation
			// intact and must not be mistaken for malformed input by the stricter encoder.
			var template = new UriTemplate("{v:2}");
			var variables = new Dictionary<string, string> { ["v"] = "a\U0001F600b" };

			template.Expand(variables).Should().Be("a%F0%9F%98%80");
		}

		[Fact]
		public void Expand_ValidAstralPlaneValueWithPrefixOne_KeepsTheWholeSurrogatePair()
		{
			// One code point is the whole pair, never a half of it.
			var template = new UriTemplate("{v:1}");
			var variables = new Dictionary<string, string> { ["v"] = "\U0001F600b" };

			template.Expand(variables).Should().Be("%F0%9F%98%80");
		}

		[Fact]
		public void Expand_ValidAstralPlaneValueWithReservedOperatorAndPrefix_IsNotRejected()
		{
			var template = new UriTemplate("{+v:2}");
			var variables = new Dictionary<string, string> { ["v"] = "a\U0001F600b" };

			template.Expand(variables).Should().Be("a%F0%9F%98%80");
		}

		// --- Coordinator decision: the apostrophe is permitted in literal text ---
		//
		// The RFC 6570 section 2.1 ABNF comment lists "'" among the excluded characters, but that
		// contradicts section 2.1's own prose ("allowed in a URI (reserved / unreserved /
		// pct-encoded)") and RFC 3986 section 2.2, which lists ' in sub-delims. The official
		// uritemplate-test suite sides with the prose: its Level 1 example requires
		// '{var}' to expand to 'value'. Every other ABNF-excluded character was audited against
		// RFC 3986 and is genuinely forbidden in a URI, so all of them stay rejected.

		[Fact]
		public void Literal_Apostrophe_IsPassedThroughUnchanged()
		{
			// The official uritemplate-test Level 1 example verbatim.
			var template = new UriTemplate("'{var}'");

			template.Expand(Variables).Should().Be("'value'");
		}

		[Fact]
		public void Literal_ApostropheOnly_IsPassedThroughUnchanged()
		{
			new UriTemplate("/it's/here").Expand(Variables).Should().Be("/it's/here");
		}

		[Theory]
		[InlineData("a\"b", "\"")]
		[InlineData("a<b", "<")]
		[InlineData("a>b", ">")]
		[InlineData("a\\b", "\\")]
		[InlineData("a^b", "^")]
		[InlineData("a`b", "`")]
		[InlineData("a|b", "|")]
		public void Literal_CharacterForbiddenByRfc3986_IsStillRejected(string template, string expected)
		{
			// Audited against RFC 3986: none of these are permitted in a URI, so unlike the
			// apostrophe they remain rejected.
			Action act = () => new UriTemplate(template);

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain($"Invalid literal character '{expected}'");
		}

		// --- Codex round 4: non-ASCII literal scalars outside ucschar / iprivate ---
		//
		// RFC 6570 section 2.1 admits non-ASCII literal text only through the ucschar and
		// iprivate productions, quoted verbatim from section 1.5 in UcsCharRanges and
		// IPrivateRanges below. A well-formed Unicode scalar outside both -- the C1 control
		// U+0085, the noncharacter U+FDD0, a plane-14 tag character -- leaves the template
		// malformed, so it must be rejected rather than percent-encoded.

		/// <summary>
		/// Independent oracle for the non-ASCII scalars accepted in literal text: the RFC 6570
		/// section 1.5 <c>ucschar</c> production, transcribed one alternative per entry.
		/// </summary>
		private static readonly (int Lo, int Hi)[] UcsCharRanges =
		{
			(0xA0, 0xD7FF),
			(0xF900, 0xFDCF),
			(0xFDF0, 0xFFEF),
			(0x10000, 0x1FFFD),
			(0x20000, 0x2FFFD),
			(0x30000, 0x3FFFD),
			(0x40000, 0x4FFFD),
			(0x50000, 0x5FFFD),
			(0x60000, 0x6FFFD),
			(0x70000, 0x7FFFD),
			(0x80000, 0x8FFFD),
			(0x90000, 0x9FFFD),
			(0xA0000, 0xAFFFD),
			(0xB0000, 0xBFFFD),
			(0xC0000, 0xCFFFD),
			(0xD0000, 0xDFFFD),
			(0xE1000, 0xEFFFD),
		};

		/// <summary>
		/// Independent oracle for the RFC 6570 section 1.5 <c>iprivate</c> production: the three
		/// Unicode private use areas.
		/// </summary>
		private static readonly (int Lo, int Hi)[] IPrivateRanges =
		{
			(0xE000, 0xF8FF),
			(0xF0000, 0xFFFFD),
			(0x100000, 0x10FFFD),
		};

		private static bool IsAcceptedLiteralScalar(int code)
		{
			foreach (var (lo, hi) in UcsCharRanges)
			{
				if (code >= lo && code <= hi)
				{
					return true;
				}
			}

			foreach (var (lo, hi) in IPrivateRanges)
			{
				if (code >= lo && code <= hi)
				{
					return true;
				}
			}

			return false;
		}

		[Fact]
		public void Literal_EveryNonAsciiScalar_MatchesUcsCharOrIPrivateSet()
		{
			for (var code = 0x80; code <= 0x10FFFF; code++)
			{
				if (code >= 0xD800 && code <= 0xDFFF)
				{
					// Surrogate code points are not scalar values and cannot appear on their own in
					// a .NET string; the unpaired-surrogate tests above cover them.
					continue;
				}

				var template = "/" + char.ConvertFromUtf32(code) + "/{var}";
				var accepted = true;

				try
				{
					new UriTemplate(template).Expand(Variables);
				}
				catch (FormatException)
				{
					accepted = false;
				}

				accepted.Should().Be(
					IsAcceptedLiteralScalar(code),
					"U+{0} is {1} the section 2.1 ucschar / iprivate set",
					code.ToString("X4"),
					IsAcceptedLiteralScalar(code) ? "inside" : "outside");
			}
		}

		[Theory]
		[InlineData(0x80)]
		[InlineData(0x85)]
		[InlineData(0x9F)]
		[InlineData(0xFDD0)]
		[InlineData(0xFDEF)]
		[InlineData(0xFFF0)]
		[InlineData(0xFFFE)]
		[InlineData(0xFFFF)]
		[InlineData(0x1FFFE)]
		[InlineData(0x1FFFF)]
		[InlineData(0xDFFFE)]
		[InlineData(0xDFFFF)]
		[InlineData(0xE0000)]
		[InlineData(0xE0001)]
		[InlineData(0xE0FFF)]
		[InlineData(0xEFFFE)]
		[InlineData(0xEFFFF)]
		[InlineData(0xFFFFE)]
		[InlineData(0xFFFFF)]
		[InlineData(0x10FFFE)]
		[InlineData(0x10FFFF)]
		public void Literal_ScalarOutsideUcsCharAndIPrivate_Rejected(int code)
		{
			// C1 controls, noncharacters, the specials block, and the plane-14 tag and
			// variation-selector block: well-formed UTF-16, but not literal text.
			Action act = () => new UriTemplate("/a" + char.ConvertFromUtf32(code) + "b/{var}");

			act.Should().Throw<FormatException>()
				.And.Message.Should().Contain("U+" + code.ToString("X4"));
		}

		[Theory]
		[InlineData(0xA0, "%C2%A0")]
		[InlineData(0xE9, "%C3%A9")]
		[InlineData(0x416, "%D0%96")]
		[InlineData(0xD7FF, "%ED%9F%BF")]
		[InlineData(0xE000, "%EE%80%80")]
		[InlineData(0xF8FF, "%EF%A3%BF")]
		[InlineData(0xF900, "%EF%A4%80")]
		[InlineData(0xFDCF, "%EF%B7%8F")]
		[InlineData(0xFDF0, "%EF%B7%B0")]
		[InlineData(0xFFEF, "%EF%BF%AF")]
		[InlineData(0x10000, "%F0%90%80%80")]
		[InlineData(0x1FFFD, "%F0%9F%BF%BD")]
		[InlineData(0x20000, "%F0%A0%80%80")]
		[InlineData(0xDFFFD, "%F3%9F%BF%BD")]
		[InlineData(0xE1000, "%F3%A1%80%80")]
		[InlineData(0xEFFFD, "%F3%AF%BF%BD")]
		[InlineData(0xF0000, "%F3%B0%80%80")]
		[InlineData(0xFFFFD, "%F3%BF%BF%BD")]
		[InlineData(0x100000, "%F4%80%80%80")]
		[InlineData(0x10FFFD, "%F4%8F%BF%BD")]
		public void Literal_ScalarInsideUcsCharOrIPrivate_EncodedAsUtf8(int code, string expected)
		{
			// The last accepted scalar before, and the first accepted scalar after, each
			// excluded range.
			var template = new UriTemplate("/" + char.ConvertFromUtf32(code) + "/{var}");

			template.Expand(Variables).Should().Be("/" + expected + "/value");
		}

		[Theory]
		[InlineData("na\u00EFve", "na%C3%AFve")] // accented Latin, mixed with ASCII
		[InlineData("\u65E5\u672C\u8A9E", "%E6%97%A5%E6%9C%AC%E8%AA%9E")] // CJK
		[InlineData("\U0001F600", "%F0%9F%98%80")] // an emoji outside the BMP
		[InlineData("\u0416\u0438\u0432", "%D0%96%D0%B8%D0%B2")] // Cyrillic
		public void Literal_OrdinaryNonAsciiText_StillEncodedAsUtf8(string text, string expected)
		{
			var template = new UriTemplate("/" + text + "/{var}");

			template.Expand(Variables).Should().Be("/" + expected + "/value");
		}
	}
}
