using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Characterization tests for the security posture of URI template expansion.
	///
	/// IMPORTANT FOR FUTURE READERS: every assertion in this file encodes
	/// RFC 6570-mandated behavior, not a defect. In particular, the reserved
	/// operators <c>{+}</c> and <c>{#}</c> are REQUIRED by RFC 6570 §3.2.3
	/// (and §1.5 / §3.2.1) to pass reserved characters and existing
	/// pct-encoded triplets through unchanged. That is the entire point of
	/// those operators: the caller has opted into supplying URI syntax.
	///
	/// These tests exist so the posture is explicit and cannot regress
	/// silently in either direction:
	///   - a change that started encoding reserved characters under {+}/{#}
	///     would break RFC compliance, and
	///   - a change that started passing control characters through under any
	///     operator would introduce a real request/header-splitting vector.
	///
	/// Do not "fix" a failing assertion here by relaxing it. If one of these
	/// fails, the implementation changed; decide deliberately which side is
	/// wrong before touching the expected value.
	/// </summary>
	public class UriTemplateSecurityTests
	{
		// ---------------------------------------------------------------
		// Reserved expansion: {+} and {#}
		// ---------------------------------------------------------------

		/// <summary>
		/// RFC 6570 §3.2.3: reserved expansion allows pct-encoded triplets and
		/// characters in the reserved set to appear literally in the result.
		/// Callers who do not want URI syntax to survive must use an operator
		/// other than <c>+</c>. This assertion encodes RFC-mandated behavior.
		/// </summary>
		[Theory]
		[InlineData("?admin=1", "http://ex.com/a?admin=1")]
		[InlineData("#frag", "http://ex.com/a#frag")]
		[InlineData("x@evil.com", "http://ex.com/ax@evil.com")]
		[InlineData("//evil.com/a", "http://ex.com/a//evil.com/a")]
		[InlineData("../../etc/passwd", "http://ex.com/a../../etc/passwd")]
		public void Plus_PassesReservedCharactersThrough(string value, string expected)
		{
			var template = new UriTemplate("http://ex.com/a{+p}");
			template.Expand(("p", value)).Should().Be(expected);
		}

		/// <summary>
		/// RFC 6570 §3.2.4: fragment expansion uses the same reserved-character
		/// rules as <c>+</c>, prefixed with '#'. This assertion encodes
		/// RFC-mandated behavior.
		/// </summary>
		[Theory]
		[InlineData("?admin=1", "http://ex.com/a#?admin=1")]
		[InlineData("x@evil.com", "http://ex.com/a#x@evil.com")]
		[InlineData("//evil.com/a", "http://ex.com/a#//evil.com/a")]
		[InlineData("../../etc/passwd", "http://ex.com/a#../../etc/passwd")]
		public void Hash_PassesReservedCharactersThrough(string value, string expected)
		{
			var template = new UriTemplate("http://ex.com/a{#p}");
			template.Expand(("p", value)).Should().Be(expected);
		}

		/// <summary>
		/// RFC 6570 §3.2.1: under reserved expansion a '%' that begins a valid
		/// pct-encoded triplet is copied literally (it is NOT re-encoded to
		/// %25), so caller-supplied encodings survive verbatim — case included.
		/// The space in "X: y" is not in the reserved set, so it still becomes
		/// %20. This assertion encodes RFC-mandated behavior.
		/// </summary>
		[Theory]
		[InlineData("%0d%0aX: y", "%0d%0aX:%20y")]
		[InlineData("%2e%2e%2fetc", "%2e%2e%2fetc")]
		public void Plus_PreEncodedTripletPassesThrough(string value, string expected)
		{
			var template = new UriTemplate("{+p}");
			template.Expand(("p", value)).Should().Be(expected);
		}

		/// <summary>
		/// RFC 6570 §3.2.2: simple string expansion percent-encodes everything
		/// outside the unreserved set, so a caller-supplied '%' becomes %25 and
		/// pre-encoded input is neutralized rather than honored. This is the
		/// safe default and is what protects templates built from untrusted
		/// input. This assertion encodes RFC-mandated behavior.
		/// </summary>
		[Theory]
		[InlineData("%0d%0aX: y", "%250d%250aX%3A%20y")]
		[InlineData("%2e%2e%2fetc", "%252e%252e%252fetc")]
		[InlineData("?admin=1", "%3Fadmin%3D1")]
		[InlineData("//evil.com/a", "%2F%2Fevil.com%2Fa")]
		[InlineData("x@evil.com", "x%40evil.com")]
		public void Unreserved_NeutralizesPreEncodedInput(string value, string expected)
		{
			var template = new UriTemplate("{p}");
			template.Expand(("p", value)).Should().Be(expected);
		}

		// ---------------------------------------------------------------
		// Query expansion
		// ---------------------------------------------------------------

		/// <summary>
		/// RFC 6570 §3.2.8: form-style query expansion encodes the value with
		/// the unreserved-only rule, so '&amp;' and '=' cannot be used to smuggle
		/// an extra query parameter into the result. This assertion encodes
		/// RFC-mandated behavior.
		/// </summary>
		[Theory]
		[InlineData("a&admin=1", "?q=a%26admin%3D1")]
		[InlineData("a#frag", "?q=a%23frag")]
		[InlineData("a;b", "?q=a%3Bb")]
		public void Query_EncodesSmugglingCharacters(string value, string expected)
		{
			var template = new UriTemplate("{?q}");
			template.Expand(("q", value)).Should().Be(expected);
		}

		// ---------------------------------------------------------------
		// Control characters
		// ---------------------------------------------------------------

		/// <summary>
		/// RFC 6570 §1.5 / §3.2.1: control characters, backslash, and non-ASCII
		/// code points are outside both the unreserved and the reserved sets, so
		/// every operator — including the reserved operators <c>+</c> and
		/// <c>#</c> — percent-encodes them (non-ASCII as UTF-8 octets). This is
		/// what makes header/request splitting impossible through expansion.
		/// This assertion encodes RFC-mandated behavior; do not relax it.
		/// </summary>
		[Theory]
		// CR + LF (header splitting)
		[InlineData("{p}", "a\r\nb", "a%0D%0Ab")]
		[InlineData("{+p}", "a\r\nb", "a%0D%0Ab")]
		[InlineData("{#p}", "a\r\nb", "#a%0D%0Ab")]
		[InlineData("{/p}", "a\r\nb", "/a%0D%0Ab")]
		[InlineData("{?p}", "a\r\nb", "?p=a%0D%0Ab")]
		[InlineData("{&p}", "a\r\nb", "&p=a%0D%0Ab")]
		[InlineData("{;p}", "a\r\nb", ";p=a%0D%0Ab")]
		[InlineData("{.p}", "a\r\nb", ".a%0D%0Ab")]
		// NUL
		[InlineData("{p}", "a\0b", "a%00b")]
		[InlineData("{+p}", "a\0b", "a%00b")]
		[InlineData("{#p}", "a\0b", "#a%00b")]
		// Backslash
		[InlineData("{p}", "a\\b", "a%5Cb")]
		[InlineData("{+p}", "a\\b", "a%5Cb")]
		[InlineData("{#p}", "a\\b", "#a%5Cb")]
		// Bidi override U+202E (UTF-8 E2 80 AE)
		[InlineData("{p}", "a\u202Eb", "a%E2%80%AEb")]
		[InlineData("{+p}", "a\u202Eb", "a%E2%80%AEb")]
		[InlineData("{#p}", "a\u202Eb", "#a%E2%80%AEb")]
		public void ControlCharactersEncodedUnderAllOperators(string template, string value, string expected)
		{
			new UriTemplate(template).Expand(("p", value)).Should().Be(expected);
		}

		// ---------------------------------------------------------------
		// Structural edge cases
		// ---------------------------------------------------------------

		/// <summary>
		/// RFC 6570 §3.2.6: path-segment expansion emits the '/' prefix and then
		/// joins parts with '/'; an empty first value contributes an empty
		/// segment, producing a leading "//". If such a template is used at
		/// position 0 of a URI reference the result is scheme-relative and
		/// resolves against the attacker-supplied authority, so callers must not
		/// build a whole URI reference starting with <c>{/...}</c> from
		/// untrusted values. The expansion itself is RFC-correct; this test
		/// pins that behavior so the hazard stays visible.
		/// </summary>
		[Fact]
		public void Slash_EmptyFirstSegmentProducesDoubleSlash()
		{
			var template = new UriTemplate("{/a,b}");
			template.Expand(("a", ""), ("b", "evil.com")).Should().Be("//evil.com");
		}

		/// <summary>
		/// A variable repeated inside a single expression — <c>{?x,x}</c> — is
		/// valid RFC 6570 and expands twice, emitting both pairs.
		///
		/// DO NOT change this assertion to expect rejection. RFC 6570 §3.2.1
		/// states, verbatim: "If a variable appears more than once in an
		/// expression or within multiple expressions of a URI Template, the
		/// value of that variable MUST remain static throughout the expansion
		/// process (i.e., the variable must have the same value for the purpose
		/// of calculating each expansion)." The clause "more than once in an
		/// expression" shows the specification explicitly contemplates
		/// repetition within one expression and imposes only a value-stability
		/// requirement on it, never a prohibition. A spec that forbade the
		/// construct would not go on to define how its value must behave.
		///
		/// §2.3 (Variables), which is sometimes cited as forbidding this,
		/// contains no such prohibition. It gives the varspec grammar
		/// (<c>variable-list = varspec *( "," varspec )</c>, which imposes no
		/// uniqueness constraint), states that names are case-sensitive, allows
		/// pct-encoded triplets in a varname, and defines when a variable counts
		/// as undefined. Nothing there — nor in §2.2 — restricts a name to a
		/// single occurrence per expression.
		///
		/// The value-stability property the RFC does impose is pinned
		/// separately by <see cref="RepeatedVariable_ExpandsToSameValueEverywhere"/>.
		///
		/// The count of emitted pairs intentionally diverges from
		/// <see cref="UriTemplate.GetVariables"/>, which deduplicates names.
		/// Recorded here so the divergence is documented as deliberate rather
		/// than discovered as a bug.
		/// </summary>
		[Fact]
		public void Query_DuplicateVarName_EmitsBoth()
		{
			var template = new UriTemplate("{?x,x}");
			template.Expand(("x", "1")).Should().Be("?x=1&x=1");
			template.GetVariables().Should().Equal("x");
		}

		/// <summary>
		/// RFC 6570 §3.2.1 requires that a variable appearing more than once —
		/// whether within one expression or across multiple expressions of the
		/// same template — "MUST remain static throughout the expansion
		/// process". This pins that property directly: <c>x</c> appears twice in
		/// one expression and again in two later expressions, and every
		/// occurrence must expand from the same supplied value.
		///
		/// This is the requirement the specification actually places on repeated
		/// variables; repetition itself is permitted (see
		/// <see cref="Query_DuplicateVarName_EmitsBoth"/>).
		/// </summary>
		[Fact]
		public void RepeatedVariable_ExpandsToSameValueEverywhere()
		{
			var template = new UriTemplate("/{x}{?x,x}{&x}");
			template.Expand(("x", "v")).Should().Be("/v?x=v&x=v&x=v");
		}

		// ---------------------------------------------------------------
		// Null values
		// ---------------------------------------------------------------

		/// <summary>
		/// RFC 6570 §2.3: a variable that is undefined — which the spec allows
		/// an implementation to treat a null value as — is ignored during
		/// expansion. This test covers only the
		/// <c>IDictionary&lt;string, object?&gt;</c> overload, whose null
		/// handling is settled. The <c>IDictionary&lt;string, string&gt;</c>
		/// overload preserves the same behavior and is pinned by
		/// <c>UriTemplateLevel1Tests.NullDictionaryValue_TreatedAsUndefined</c>.
		/// The <c>IDictionary&lt;string, UriTemplateValue&gt;</c> overload still
		/// throws for a null value today; aligning it with the others is issue
		/// #21, with the fix and its coverage pending in PR #41.
		/// </summary>
		[Fact]
		public void NullValue_TreatedAsUndefined()
		{
			var template = new UriTemplate("{a}{b}");
			var variables = new Dictionary<string, object?>
			{
				["a"] = null,
				["b"] = "v",
			};

			template.Expand(variables).Should().Be("v");
		}

		/// <summary>
		/// RFC 6570 §2.3 / §3.2.1: an undefined variable contributes nothing at
		/// all, so a null value under a named operator does not even emit the
		/// name. Same overload caveat as
		/// <see cref="NullValue_TreatedAsUndefined"/>.
		/// </summary>
		[Fact]
		public void NullValue_TreatedAsUndefined_NamedOperator()
		{
			var template = new UriTemplate("{?a,b}");
			var variables = new Dictionary<string, object?>
			{
				["a"] = null,
				["b"] = "v",
			};

			template.Expand(variables).Should().Be("?b=v");
		}
	}
}
