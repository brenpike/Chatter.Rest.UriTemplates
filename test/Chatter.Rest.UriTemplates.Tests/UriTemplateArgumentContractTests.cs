using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Argument-contract tests for <see cref="UriTemplate"/>'s public overloads:
	/// at-most-once reading of caller-supplied sequences, the order in which failures
	/// surface relative to the values a template names, consistent null handling, explicit
	/// null-key reporting, and validation of a custom parser's result.
	/// </summary>
	public class UriTemplateArgumentContractTests
	{
		// ----------------------------------------------------------------
		// 1. Caller-supplied enumerables are read at most once
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_ObjectDictionary_SinglePassList_UsedByEveryExpression()
		{
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?> { ["list"] = oneShot };
			var template = new UriTemplate("{list}{?list*}");

			template.Expand(vars).Should().Be("red,green?list=red&list=green");
			oneShot.EnumerationCount.Should().Be(1);
		}

		[Fact]
		public void Expand_ObjectTuple_SinglePassList_UsedByEveryExpression()
		{
			var oneShot = new SinglePassList("red", "green");
			var template = new UriTemplate("{list}{?list*}");

			template.Expand(("list", (object?)oneShot)).Should().Be("red,green?list=red&list=green");
			oneShot.EnumerationCount.Should().Be(1);
		}

		[Fact]
		public void Expand_ObjectDictionary_SinglePassAssociativeArray_UsedByEveryExpression()
		{
			var oneShot = new SinglePassPairs(
				new KeyValuePair<string, string>("semi", ";"),
				new KeyValuePair<string, string>("dot", "."));
			var vars = new Dictionary<string, object?> { ["keys"] = oneShot };
			var template = new UriTemplate("{keys}{?keys*}");

			template.Expand(vars).Should().Be("semi,%3B,dot,.?semi=%3B&dot=.");
			oneShot.EnumerationCount.Should().Be(1);
		}

		[Fact]
		public void Expand_ObjectTuple_SinglePassAssociativeArray_UsedByEveryExpression()
		{
			var oneShot = new SinglePassPairs(
				new KeyValuePair<string, string>("semi", ";"),
				new KeyValuePair<string, string>("dot", "."));
			var template = new UriTemplate("{keys}{?keys*}");

			template.Expand(("keys", (object?)oneShot)).Should().Be("semi,%3B,dot,.?semi=%3B&dot=.");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// A sequence whose contents change between enumerations must not be able to
		// give two expressions of the same template two different answers.
		[Fact]
		public void Expand_ObjectDictionary_ChangingSequence_ObservedIdenticallyByEveryExpression()
		{
			var changing = new ChangingList(new[] { "first" }, new[] { "second" });
			var vars = new Dictionary<string, object?> { ["v"] = changing };
			var template = new UriTemplate("{v}/{v}");

			template.Expand(vars).Should().Be("first/first");
		}

		// Mutating the caller's collection after Expand returns must not be observable,
		// and a later Expand must see the mutation.
		[Fact]
		public void Expand_ObjectDictionary_CallerMutationAfterExpand_DoesNotAffectResult()
		{
			var list = new List<string> { "a" };
			var vars = new Dictionary<string, object?> { ["v"] = list };
			var template = new UriTemplate("{v}");

			var before = template.Expand(vars);
			list.Add("b");
			var after = template.Expand(vars);

			before.Should().Be("a");
			after.Should().Be("a,b");
		}

		// A value the template never references must not be read at all: reading it
		// would turn an unused lazy, blocking, or infinite sequence into a failure or a
		// hang for an expansion that has no use for it. Wrapping it is free — the wrapper
		// materializes on its first enumeration, and no enumeration ever comes.
		[Fact]
		public void Expand_ObjectDictionary_UnreferencedThrowingSequence_IsNeverEnumerated()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?> { ["unused"] = landmine };
			var template = new UriTemplate("/status");

			template.Expand(vars).Should().Be("/status");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_UnreferencedThrowingSequence_IsNeverEnumerated()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("/orders/{id}");

			template.Expand(("id", (object?)"42"), ("unused", (object?)landmine))
				.Should().Be("/orders/42");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// Leaving unreferenced values unread must not weaken the at-most-once guarantee
		// for the values the template does reference.
		[Fact]
		public void Expand_ObjectDictionary_ReferencedSequenceStillReadOnce_WhenUnreferencedValuePresent()
		{
			var oneShot = new SinglePassList("red", "green");
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["list"] = oneShot,
				["unused"] = landmine,
			};
			var template = new UriTemplate("{list}{?list*}");

			template.Expand(vars).Should().Be("red,green?list=red&list=green");
			oneShot.EnumerationCount.Should().Be(1);
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// ----------------------------------------------------------------
		// 1b. Materializing must not pre-empt the expander's own validation
		// ----------------------------------------------------------------

		// Dictionary<,> rejects a null key with ArgumentNullException. Recording a
		// caller's IDictionary through the copy constructor would therefore replace the
		// documented, variable-specific FormatException with a different exception type.
		[Fact]
		public void Expand_ObjectDictionary_DictionaryWithNullKey_ThrowsFormatExceptionNamingVariable()
		{
			var vars = new Dictionary<string, object?> { ["keys"] = new NullKeyDictionary() };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null key*");
		}

		[Fact]
		public void Expand_ObjectTuple_DictionaryWithNullKey_ThrowsFormatExceptionNamingVariable()
		{
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(("keys", (object?)new NullKeyDictionary()));

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null key*");
		}

		// ----------------------------------------------------------------
		// 1c. Materializing must not pre-empt the expander's prefix validation
		// ----------------------------------------------------------------

		// RFC 6570 forbids a prefix modifier on a composite value, and the expander
		// rejects it before it ever reads the value. Materializing a referenced value
		// eagerly would enumerate the sequence first, turning that documented
		// FormatException into whatever the sequence happens to do — an arbitrary
		// exception, a block, or a non-terminating enumeration.
		[Fact]
		public void Expand_ObjectDictionary_PrefixOnCompositeSequence_ThrowsFormatExceptionWithoutEnumerating()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?> { ["items"] = landmine };
			var template = new UriTemplate("{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'items'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_PrefixOnCompositeSequence_ThrowsFormatExceptionWithoutEnumerating()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{items:3}");

			var act = () => template.Expand(("items", (object?)landmine));

			act.Should().Throw<FormatException>()
				.WithMessage("*'items'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectDictionary_PrefixOnCompositeAssociativeArray_ThrowsFormatExceptionWithoutEnumerating()
		{
			var landmine = new ThrowingDictionary();
			var vars = new Dictionary<string, object?> { ["items"] = landmine };
			var template = new UriTemplate("{?items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'items'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// The prefix modifier need not be on the first expression, and a prefixed
		// reference reads nothing wherever it sits: the composite behind it is left
		// alone even though a later expression names it without a prefix.
		[Fact]
		public void Expand_ObjectDictionary_PrefixOnEarlierReference_ThrowsFormatExceptionWithoutEnumerating()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?> { ["items"] = landmine };
			var template = new UriTemplate("{items:3}{items}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(PrefixOnCompositeMessage("items"));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// A prefix modifier on a plain string is legal, and a composite-shaped value
		// must not be rejected when no reference to it carries a prefix.
		[Fact]
		public void Expand_ObjectDictionary_PrefixOnStringValue_IsUnaffectedByCompositeValidation()
		{
			var vars = new Dictionary<string, object?>
			{
				["s"] = "value",
				["items"] = new List<string> { "red", "green" },
			};
			var template = new UriTemplate("{s:3}{items}");

			template.Expand(vars).Should().Be("valred,green");
		}

		// ----------------------------------------------------------------
		// 1c-2. Validation follows template order, not caller order
		// ----------------------------------------------------------------

		// Validation must be driven by the template's expressions, not by the order the
		// caller happened to supply the values. In "{bad:1}{other}" the prefix modifier
		// on 'bad' makes the expansion fail before 'other' is ever needed, so supplying
		// 'other' first must not cause it to be materialized: a lazy sequence there could
		// throw something unrelated, block, or never return.
		[Fact]
		public void Expand_ObjectTuple_PrefixViolationInEarlierExpression_PreemptsLaterValueSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad:1}{other}");

			var act = () => template.Expand(
				("other", (object?)landmine),
				("bad", (object?)new[] { "x" }));

			act.Should().Throw<FormatException>()
				.WithMessage("*'bad'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// The mirror of the case above: with the caller's order reversed the outcome must
		// be identical, which is what proves the behaviour is order-independent rather
		// than accidentally correct for one arrangement.
		[Fact]
		public void Expand_ObjectTuple_PrefixViolationInEarlierExpression_PreemptsLaterValueSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad:1}{other}");

			var act = () => template.Expand(
				("bad", (object?)new[] { "x" }),
				("other", (object?)landmine));

			act.Should().Throw<FormatException>()
				.WithMessage("*'bad'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// The dictionary overload iterates the caller's dictionary, so it is subject to
		// the same ordering hazard.
		[Fact]
		public void Expand_ObjectDictionary_PrefixViolationInEarlierExpression_PreemptsLaterValueSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["other"] = landmine,
				["bad"] = new[] { "x" },
			};
			var template = new UriTemplate("{bad:1}{other}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'bad'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectDictionary_PrefixViolationInEarlierExpression_PreemptsLaterValueSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["bad"] = new[] { "x" },
				["other"] = landmine,
			};
			var template = new UriTemplate("{bad:1}{other}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'bad'*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// A null key is only detectable by enumerating, so it is reported when the value is
		// materialized — which means materialization has to happen in template order, or a
		// later variable's value could be read on the way to an earlier variable's failure.
		// It does, because the enumeration that materializes a value is the expander's own.
		[Fact]
		public void Expand_ObjectTuple_NullKeyInEarlierExpression_PreemptsLaterValueSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{?keys*}{other}");

			var act = () => template.Expand(
				("other", (object?)landmine),
				("keys", (object?)new NullKeyDictionary()));

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null key*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_NullKeyInEarlierExpression_PreemptsLaterValueSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{?keys*}{other}");

			var act = () => template.Expand(
				("keys", (object?)new NullKeyDictionary()),
				("other", (object?)landmine));

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null key*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// ----------------------------------------------------------------
		// 1d. A null key must not cost the caller a second enumeration
		// ----------------------------------------------------------------

		// Falling back to the caller's instance after the value was already read once
		// would hand the expander a drained sequence: a single-pass dictionary would then
		// surface InvalidOperationException from its second enumeration instead of the
		// documented variable-specific FormatException.
		[Fact]
		public void Expand_ObjectDictionary_SinglePassDictionaryWithNullKey_ThrowsFormatExceptionNamingVariable()
		{
			var oneShot = new SinglePassNullKeyDictionary();
			var vars = new Dictionary<string, object?> { ["keys"] = oneShot };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null key*");
			oneShot.EnumerationCount.Should().Be(1);
		}

		[Fact]
		public void Expand_ObjectTuple_SinglePassDictionaryWithNullKey_ThrowsFormatExceptionNamingVariable()
		{
			var oneShot = new SinglePassNullKeyDictionary();
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(("keys", (object?)oneShot));

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null key*");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// A null value carries its own variable-specific FormatException, which the
		// memoizing view preserves by recording null values untouched.
		[Fact]
		public void Expand_ObjectDictionary_SinglePassDictionaryWithNullValue_ThrowsFormatExceptionNamingVariable()
		{
			var oneShot = new SinglePassDictionary(
				new KeyValuePair<string, string>("ok", null!));
			var vars = new Dictionary<string, object?> { ["keys"] = oneShot };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*'keys'*null value*");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// The view must keep dictionary values dispatching down the expander's
		// IDictionary<string, string> branch rather than the insertion-ordered
		// IEnumerable<KeyValuePair<,>> branch.
		[Fact]
		public void Expand_ObjectDictionary_SinglePassDictionary_ReachesExpanderAsDictionary()
		{
			var oneShot = new SinglePassDictionary(
				new KeyValuePair<string, string>("semi", ";"),
				new KeyValuePair<string, string>("dot", "."));
			var vars = new Dictionary<string, object?> { ["keys"] = oneShot };
			var template = new UriTemplate("{?keys*}{&keys*}");

			var result = template.Expand(vars);

			result.Should().Contain("semi=%3B").And.Contain("dot=.");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// ----------------------------------------------------------------
		// 1c-3. An earlier variable's failure pre-empts every later variable
		// ----------------------------------------------------------------

		// The expander is the only place that knows a value's type is unsupported, so with
		// validation split into phases the check ran after every value had been read:
		// "{bad}{later}" reported whatever 'later' did on enumeration instead of the
		// documented FormatException for 'bad'. Leaving every value unread until the
		// expander enumerates it puts the failure back where the template says it belongs.
		[Fact]
		public void Expand_ObjectTuple_UnsupportedValueInEarlierExpression_PreemptsLaterValueSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad}{later}");

			var act = () => template.Expand(
				("later", (object?)landmine),
				("bad", (object?)42));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_UnsupportedValueInEarlierExpression_PreemptsLaterValueSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad}{later}");

			var act = () => template.Expand(
				("bad", (object?)42),
				("later", (object?)landmine));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueInEarlierExpression_PreemptsLaterValueSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["later"] = landmine,
				["bad"] = 42,
			};
			var template = new UriTemplate("{bad}{later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueInEarlierExpression_PreemptsLaterValueSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["bad"] = 42,
				["later"] = landmine,
			};
			var template = new UriTemplate("{bad}{later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// The same precedence has to hold against a validation that reads nothing at all.
		// A prefix-validation pass over the whole template reported 'items' for
		// "{bad}{items:3}" even though 'bad' fails first in template order; the prefix rule
		// is applied by the expander at the prefixed variable's own turn, after 'bad'.
		[Fact]
		public void Expand_ObjectTuple_UnsupportedValueInEarlierExpression_PreemptsLaterPrefixViolationSuppliedFirst()
		{
			var template = new UriTemplate("{bad}{items:3}");

			var act = () => template.Expand(
				("items", (object?)new[] { "x" }),
				("bad", (object?)42));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectTuple_UnsupportedValueInEarlierExpression_PreemptsLaterPrefixViolationSuppliedSecond()
		{
			var template = new UriTemplate("{bad}{items:3}");

			var act = () => template.Expand(
				("bad", (object?)42),
				("items", (object?)new[] { "x" }));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueInEarlierExpression_PreemptsLaterPrefixViolationSuppliedFirst()
		{
			var vars = new Dictionary<string, object?>
			{
				["items"] = new[] { "x" },
				["bad"] = 42,
			};
			var template = new UriTemplate("{bad}{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueInEarlierExpression_PreemptsLaterPrefixViolationSuppliedSecond()
		{
			var vars = new Dictionary<string, object?>
			{
				["bad"] = 42,
				["items"] = new[] { "x" },
			};
			var template = new UriTemplate("{bad}{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		// The mirror image: when the prefixed variable is the one the template names
		// first, it is the one reported, and the unsupported value behind it is never
		// reached.
		[Fact]
		public void Expand_ObjectDictionary_PrefixViolationBeforeUnsupportedValue_ReportsThePrefixedVariable()
		{
			var vars = new Dictionary<string, object?>
			{
				["bad"] = 42,
				["items"] = new[] { "x" },
			};
			var template = new UriTemplate("{items:3}{bad}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(
					"Prefix modifier is not applicable to composite values per RFC 6570 (variable 'items').");
		}

		// An unsupported type must still be reported when it is the only problem, with
		// the expander's message — the check simply happens earlier now.
		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValue_ThrowsExpandersFormatException()
		{
			var vars = new Dictionary<string, object?> { ["bad"] = 42 };
			var template = new UriTemplate("{bad}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		// An unsupported value bound to a name the template never mentions is not a
		// failure: nothing is validated for a variable that is never referenced.
		[Fact]
		public void Expand_ObjectDictionary_UnreferencedUnsupportedValue_IsIgnored()
		{
			var vars = new Dictionary<string, object?> { ["unused"] = 42 };
			var template = new UriTemplate("/status");

			template.Expand(vars).Should().Be("/status");
		}

		// ----------------------------------------------------------------
		// 1e. Composite members are validated as they are copied
		// ----------------------------------------------------------------

		// Recording a composite first and validating it afterwards keeps reading past the
		// first invalid member, so a null followed by a throwing, blocking, or endless
		// remainder never produced the documented FormatException. Each of the four cases
		// below stops at the offending member; the double records whether the remainder
		// was ever reached.
		[Fact]
		public void Expand_ObjectDictionary_DictionaryWithNullValueThenHostileRemainder_ThrowsFormatExceptionAtTheNull()
		{
			var hostile = HostileDictionary.After(new KeyValuePair<string, string>("ok", null!));
			var vars = new Dictionary<string, object?> { ["keys"] = hostile };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullValueMessage("keys", "ok"));
			hostile.ReachedRemainder.Should().BeFalse();
		}

		[Fact]
		public void Expand_ObjectDictionary_DictionaryWithNullKeyThenHostileRemainder_ThrowsFormatExceptionAtTheNull()
		{
			var hostile = HostileDictionary.After(new KeyValuePair<string, string>(null!, "1"));
			var vars = new Dictionary<string, object?> { ["keys"] = hostile };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullKeyMessage("keys"));
			hostile.ReachedRemainder.Should().BeFalse();
		}

		[Fact]
		public void Expand_ObjectDictionary_PairsWithNullKeyThenHostileRemainder_ThrowsFormatExceptionAtTheNull()
		{
			var hostile = new HostilePairs(new KeyValuePair<string, string>(null!, "1"));
			var vars = new Dictionary<string, object?> { ["keys"] = hostile };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullKeyMessage("keys"));
			hostile.ReachedRemainder.Should().BeFalse();
		}

		[Fact]
		public void Expand_ObjectDictionary_PairsWithNullValueThenHostileRemainder_ThrowsFormatExceptionAtTheNull()
		{
			var hostile = new HostilePairs(new KeyValuePair<string, string>("ok", null!));
			var vars = new Dictionary<string, object?> { ["keys"] = hostile };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullValueMessage("keys", "ok"));
			hostile.ReachedRemainder.Should().BeFalse();
		}

		[Fact]
		public void Expand_ObjectDictionary_ListWithNullElementThenHostileRemainder_ThrowsFormatExceptionAtTheNull()
		{
			var hostile = new HostileList("red", null!);
			var vars = new Dictionary<string, object?> { ["items"] = hostile };
			var template = new UriTemplate("{items}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullElementMessage("items"));
			hostile.ReachedRemainder.Should().BeFalse();
		}

		[Fact]
		public void Expand_ObjectTuple_ListWithNullElementThenHostileRemainder_ThrowsFormatExceptionAtTheNull()
		{
			var hostile = new HostileList("red", null!);
			var template = new UriTemplate("{items}");

			var act = () => template.Expand(("items", (object?)hostile));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullElementMessage("items"));
			hostile.ReachedRemainder.Should().BeFalse();
		}

		// The member validation is part of the variable's own turn in the walk, so it is
		// still ordered by the template rather than by the caller's argument list.
		[Fact]
		public void Expand_ObjectTuple_NullElementInEarlierExpression_PreemptsLaterValueSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{items}{other}");

			var act = () => template.Expand(
				("other", (object?)landmine),
				("items", (object?)new HostileList("red", null!)));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullElementMessage("items"));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_NullElementInEarlierExpression_PreemptsLaterValueSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{items}{other}");

			var act = () => template.Expand(
				("items", (object?)new HostileList("red", null!)),
				("other", (object?)landmine));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullElementMessage("items"));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// ----------------------------------------------------------------
		// 1f. A set of pairs stays a set on its way to the expander
		// ----------------------------------------------------------------

		// UriTemplateExpander decides associative-array ordering by runtime type, and a
		// set has no order of its own to preserve. Wrapping a set in a bare pair sequence —
		// as the general pairs branch does — would present it as an ordered sequence and
		// make the expansion depend on the caller's set implementation. The view therefore
		// has to arrive still implementing ISet<KeyValuePair<string, string>>.
		[Fact]
		public void Expand_ObjectDictionary_SetOfPairs_ReachesExpanderAsSet()
		{
			var recorder = new RecordingExpander();
			var set = new HashSet<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("semi", ";"),
				new KeyValuePair<string, string>("dot", "."),
			};
			var vars = new Dictionary<string, object?> { ["keys"] = set };
			var template = new UriTemplate("{?keys*}", UriTemplateParser.Default, recorder);

			template.Expand(vars);

			recorder.Captured.Should().BeAssignableTo<ISet<KeyValuePair<string, string>>>();
			recorder.Captured.Should().NotBeSameAs(set);
			((ISet<KeyValuePair<string, string>>)recorder.Captured!).Should().BeEquivalentTo(set);
		}

		[Fact]
		public void Expand_ObjectTuple_SetOfPairs_ReachesExpanderAsSet()
		{
			var recorder = new RecordingExpander();
			var set = new HashSet<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("semi", ";"),
			};
			var template = new UriTemplate("{?keys*}", UriTemplateParser.Default, recorder);

			template.Expand(("keys", (object?)set));

			recorder.Captured.Should().BeAssignableTo<ISet<KeyValuePair<string, string>>>();
			recorder.Captured.Should().NotBeSameAs(set);
		}

		// A custom ISet implementation must be recognized by its interface, not by being
		// a HashSet.
		[Fact]
		public void Expand_ObjectDictionary_CustomSetOfPairs_ReachesExpanderAsSet()
		{
			var recorder = new RecordingExpander();
			var set = new SingleEntryPairSet(new KeyValuePair<string, string>("semi", ";"));
			var vars = new Dictionary<string, object?> { ["keys"] = set };
			var template = new UriTemplate("{?keys*}", UriTemplateParser.Default, recorder);

			template.Expand(vars);

			recorder.Captured.Should().BeAssignableTo<ISet<KeyValuePair<string, string>>>();
			recorder.Captured.Should().NotBeSameAs(set);
		}

		// The set branch owes the same guarantees as every other composite branch: it is
		// read once, and its members are validated as they are recorded.
		[Fact]
		public void Expand_ObjectDictionary_SetOfPairs_ExpandsThroughTheRealExpander()
		{
			var set = new HashSet<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("semi", ";"),
			};
			var vars = new Dictionary<string, object?> { ["keys"] = set };
			var template = new UriTemplate("{?keys*}");

			template.Expand(vars).Should().Be("?semi=%3B");
		}

		[Fact]
		public void Expand_ObjectDictionary_SetOfPairsWithNullValue_ThrowsFormatExceptionNamingVariable()
		{
			var set = new HashSet<KeyValuePair<string, string>>
			{
				new KeyValuePair<string, string>("ok", null!),
			};
			var vars = new Dictionary<string, object?> { ["keys"] = set };
			var template = new UriTemplate("{?keys*}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullValueMessage("keys", "ok"));
		}

		// A caller's set may be built with an equality comparer finer than
		// EqualityComparer<KeyValuePair<string, string>>.Default — reference identity,
		// for instance — and may therefore legally hold two entries the default relation
		// considers equal. The view has to keep every entry it observed: recording into
		// a HashSet<KeyValuePair<string, string>> imposes the default relation and
		// silently drops one of them, changing the expanded URI.
		[Fact]
		public void Expand_ObjectDictionary_SetWithFinerEquality_KeepsEveryObservedEntry()
		{
			// Distinct instances with equal content: a reference-identity comparer keeps
			// both, the default comparer merges them.
			var first = new string('.', 1);
			var second = new string('.', 1);

			// The premise the test rests on, asserted rather than assumed.
			ReferenceEquals(first, second).Should().BeFalse();
			EqualityComparer<KeyValuePair<string, string>>.Default.Equals(
					new KeyValuePair<string, string>("dot", first),
					new KeyValuePair<string, string>("dot", second))
				.Should().BeTrue();

			var set = new SingleEntryPairSet(
				new KeyValuePair<string, string>("semi", ";"),
				new KeyValuePair<string, string>("dot", first),
				new KeyValuePair<string, string>("dot", second));
			var vars = new Dictionary<string, object?> { ["keys"] = set };
			var template = new UriTemplate("{?keys*}");

			// Both 'dot' entries survive, and the pairs arrive in the canonical order the
			// expander imposes on a set — ordinal by key — not the set's own.
			template.Expand(vars).Should().Be("?dot=.&dot=.&semi=%3B");
		}

		// The view keeps every observed entry, but it must still present as a set, or the
		// expander's ordered-sequence branch would take over and the result would depend on
		// the caller's set implementation after all.
		[Fact]
		public void Expand_ObjectDictionary_SetWithFinerEquality_StillReachesExpanderAsSet()
		{
			var recorder = new RecordingExpander();
			var set = new SingleEntryPairSet(
				new KeyValuePair<string, string>("dot", new string('.', 1)),
				new KeyValuePair<string, string>("dot", new string('.', 1)));
			var vars = new Dictionary<string, object?> { ["keys"] = set };
			var template = new UriTemplate("{?keys*}", UriTemplateParser.Default, recorder);

			template.Expand(vars);

			recorder.Captured.Should().BeAssignableTo<ISet<KeyValuePair<string, string>>>();
			recorder.Captured.Should().NotBeSameAs(set);
			((ISet<KeyValuePair<string, string>>)recorder.Captured!).Count.Should().Be(2);
		}

		// The same defect as the set case, one type over. A caller's dictionary may be
		// built with a comparer finer than StringComparer.Ordinal — reference identity,
		// for instance — and may then legally hold two entries whose keys have equal text
		// but are distinct instances. Recording into an ordinal Dictionary<string, string>
		// collapses them and the expanded URI loses a member, where the expander had
		// enumerated and canonicalized both.
		[Fact]
		public void Expand_ObjectDictionary_DictionaryWithFinerKeyEquality_KeepsEveryObservedEntry()
		{
			// Distinct instances with equal text: a reference-identity comparer keeps both
			// as separate keys, an ordinal one does not.
			var firstKey = new string("dot".ToCharArray());
			var secondKey = new string("dot".ToCharArray());

			// The premise the test rests on, asserted rather than assumed.
			ReferenceEquals(firstKey, secondKey).Should().BeFalse();
			StringComparer.Ordinal.Equals(firstKey, secondKey).Should().BeTrue();

			var source = new ReferenceKeyedDictionary(
				new KeyValuePair<string, string>("semi", ";"),
				new KeyValuePair<string, string>(firstKey, "."),
				new KeyValuePair<string, string>(secondKey, "."));

			// The source really does hold all three, so anything lost is lost by the copy.
			source.Count.Should().Be(3);

			var vars = new Dictionary<string, object?> { ["keys"] = source };
			var template = new UriTemplate("{?keys*}");

			// Both 'dot' entries survive, and the pairs arrive in the canonical order the
			// expander imposes on a keyed map, not the dictionary's own.
			template.Expand(vars).Should().Be("?dot=.&dot=.&semi=%3B");
		}

		// Keeping every entry must not cost the dictionary its dispatch: the view has to
		// stay on the expander's IDictionary branch, or the canonical ordering the branch
		// exists to apply is lost.
		[Fact]
		public void Expand_ObjectDictionary_DictionaryWithFinerKeyEquality_StillReachesExpanderAsDictionary()
		{
			var recorder = new RecordingExpander();
			var source = new ReferenceKeyedDictionary(
				new KeyValuePair<string, string>(new string("dot".ToCharArray()), "."),
				new KeyValuePair<string, string>(new string("dot".ToCharArray()), "."));
			var vars = new Dictionary<string, object?> { ["keys"] = source };
			var template = new UriTemplate("{?keys*}", UriTemplateParser.Default, recorder);

			template.Expand(vars);

			recorder.Captured.Should().BeAssignableTo<IDictionary<string, string>>();
			recorder.Captured.Should().NotBeSameAs(source);
			((IDictionary<string, string>)recorder.Captured!).Count.Should().Be(2);
		}

		// A dictionary is checked before a set, so a type that is somehow both still
		// takes the expander's IDictionary branch.
		[Fact]
		public void Expand_ObjectDictionary_Dictionary_StillReachesExpanderAsDictionary()
		{
			var recorder = new RecordingExpander();
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new Dictionary<string, string> { ["semi"] = ";" },
			};
			var template = new UriTemplate("{?keys*}", UriTemplateParser.Default, recorder);

			template.Expand(vars);

			recorder.Captured.Should().BeAssignableTo<IDictionary<string, string>>();
		}

		// ----------------------------------------------------------------
		// 1c-4. A prefix violation belongs to the reference that carries it
		// ----------------------------------------------------------------

		// A prefix modifier over a composite is the prefixed reference's failure, and it
		// is raised where that reference sits — not hoisted to the variable's first
		// mention. Hoisting it let it jump ahead of variables the template names in
		// between: "{items}{bad}{items:3}" reported 'items' when 'bad' fails first in
		// template order.
		[Fact]
		public void Expand_ObjectDictionary_PrefixViolation_DoesNotPreemptInterveningUnsupportedValueSuppliedFirst()
		{
			var vars = new Dictionary<string, object?>
			{
				["items"] = new[] { "x" },
				["bad"] = 42,
			};
			var template = new UriTemplate("{items}{bad}{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectDictionary_PrefixViolation_DoesNotPreemptInterveningUnsupportedValueSuppliedSecond()
		{
			var vars = new Dictionary<string, object?>
			{
				["bad"] = 42,
				["items"] = new[] { "x" },
			};
			var template = new UriTemplate("{items}{bad}{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectTuple_PrefixViolation_DoesNotPreemptInterveningUnsupportedValueSuppliedFirst()
		{
			var template = new UriTemplate("{items}{bad}{items:3}");

			var act = () => template.Expand(
				("items", (object?)new[] { "x" }),
				("bad", (object?)42));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectTuple_PrefixViolation_DoesNotPreemptInterveningUnsupportedValueSuppliedSecond()
		{
			var template = new UriTemplate("{items}{bad}{items:3}");

			var act = () => template.Expand(
				("bad", (object?)42),
				("items", (object?)new[] { "x" }));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		// "{items}{items:3}" reads the value once and then reports the prefix violation.
		// The first reference is a legal, unprefixed one that genuinely needs the value in
		// order to expand, so expanding it is not a defect — it is what the template asks
		// for. What is guaranteed is that the prefixed reference itself reads nothing: the
		// count is one, the enumeration the first expression performed, and the single-pass
		// value is not drained a second time on the way to the failure.
		[Fact]
		public void Expand_ObjectDictionary_PrefixOnLaterReference_ReportsPrefixErrorAfterTheUnprefixedReferenceReadsTheValue()
		{
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?> { ["items"] = oneShot };
			var template = new UriTemplate("{items}{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(PrefixOnCompositeMessage("items"));
			oneShot.EnumerationCount.Should().Be(1);
		}

		[Fact]
		public void Expand_ObjectTuple_PrefixOnLaterReference_ReportsPrefixErrorAfterTheUnprefixedReferenceReadsTheValue()
		{
			var oneShot = new SinglePassList("red", "green");
			var template = new UriTemplate("{items}{items:3}");

			var act = () => template.Expand(("items", (object?)oneShot));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(PrefixOnCompositeMessage("items"));
			oneShot.EnumerationCount.Should().Be(1);
		}

		// A prefix violation cannot be lost: the expander visits every varspec of every
		// expression, so it must arrive at the prefixed one unless something earlier
		// throws first. An intervening variable that expands cleanly does not absolve it,
		// and it does not cost the value a second enumeration either.
		[Fact]
		public void Expand_ObjectDictionary_PrefixViolation_IsStillReportedAfterAValidInterveningVariable()
		{
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?>
			{
				["items"] = oneShot,
				["other"] = "ok",
			};
			var template = new UriTemplate("{items}{other}{items:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(PrefixOnCompositeMessage("items"));
			oneShot.EnumerationCount.Should().Be(1);
		}

		// The type check precedes the prefix rule for one and the same variable, so an
		// unsupported value under a prefixed reference is reported as the unsupported
		// type it is, not as a misapplied modifier.
		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueUnderALaterPrefix_ReportsTheTypeNotThePrefix()
		{
			var vars = new Dictionary<string, object?> { ["bad"] = 42 };
			var template = new UriTemplate("{bad}{bad:3}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		[Fact]
		public void Expand_ObjectTuple_UnsupportedValueUnderALaterPrefix_ReportsTheTypeNotThePrefix()
		{
			var template = new UriTemplate("{bad}{bad:3}");

			var act = () => template.Expand(("bad", (object?)42));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		// The value is materialized by the first reference that reads it and replayed for
		// every later one: a variable named three times is still read exactly once.
		[Fact]
		public void Expand_ObjectDictionary_ThreeReferences_MaterializeTheValueOnce()
		{
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?> { ["list"] = oneShot };
			var template = new UriTemplate("{list}/{list}{?list*}");

			template.Expand(vars).Should().Be("red,green/red,green?list=red&list=green");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// ----------------------------------------------------------------
		// 1c-5. A failure raised inside the encoder pre-empts later values too
		// ----------------------------------------------------------------

		// An unpaired UTF-16 surrogate in a scalar is rejected by the encoder, which runs
		// during expansion and nowhere else. Preparing every value before expanding
		// anything therefore put a later composite's enumeration ahead of it, and no
		// amount of validation added to a preparation pass would have covered the next such
		// failure. Preparing nothing removes the class outright: the encoder fires while
		// the first expression is being expanded, and the second expression's values are
		// still unread because nothing has enumerated them.
		[Fact]
		public void Expand_ObjectDictionary_UnencodableStringInEarlierExpression_PreemptsLaterValue()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["bad"] = "\uD800",
				["later"] = landmine,
			};
			var template = new UriTemplate("{bad}{later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*unpaired UTF-16 surrogate*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_UnencodableStringInEarlierExpression_PreemptsLaterValue()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad}{later}");

			var act = () => template.Expand(
				("later", (object?)landmine),
				("bad", (object?)"\uD800"));

			act.Should().Throw<FormatException>()
				.WithMessage("*unpaired UTF-16 surrogate*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// The same holds for the prefixed form, which validates the whole value before
		// truncating it, and for a surrogate that a naive truncation would have discarded.
		[Fact]
		public void Expand_ObjectDictionary_UnencodableStringUnderAPrefixInEarlierExpression_PreemptsLaterValue()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["bad"] = "a\uD800",
				["later"] = landmine,
			};
			var template = new UriTemplate("{bad:1}{later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*unpaired UTF-16 surrogate*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// ----------------------------------------------------------------
		// 1c-6. A failure pre-empts every later varspec of its own expression
		// ----------------------------------------------------------------

		// The guarantee is unconditional, not expression-granular. Preparing one
		// expression's variables immediately before expanding it closed the gap between
		// expressions but not the one inside them: in "{bad,later}" both varspecs belong to
		// the same expression, so 'later' was still read on the way to reporting 'bad'.
		// Nothing prepares anything now. A composite value is wrapped, unread, in a view
		// that materializes on its first enumeration, and the only thing that enumerates it
		// is the expander reaching the varspec that names it — so a varspec the expander
		// never reaches leaves its value untouched, wherever that varspec sits.
		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueInSameExpression_PreemptsLaterVarSpecSuppliedFirst()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["later"] = landmine,
				["bad"] = 42,
			};
			var template = new UriTemplate("{bad,later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectDictionary_UnsupportedValueInSameExpression_PreemptsLaterVarSpecSuppliedSecond()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["bad"] = 42,
				["later"] = landmine,
			};
			var template = new UriTemplate("{bad,later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_UnsupportedValueInSameExpression_PreemptsLaterVarSpec()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad,later}");

			var act = () => template.Expand(
				("later", (object?)landmine),
				("bad", (object?)42));

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// The encoder case, one expression over. The encoder runs inside the expander while
		// the first varspec is being expanded, which is still ahead of the second varspec's
		// value being enumerated.
		[Fact]
		public void Expand_ObjectDictionary_UnencodableStringInSameExpression_PreemptsLaterVarSpec()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["bad"] = "\uD800",
				["later"] = landmine,
			};
			var template = new UriTemplate("{bad,later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.WithMessage("*unpaired UTF-16 surrogate*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_UnencodableStringInSameExpression_PreemptsLaterVarSpec()
		{
			var landmine = new ThrowingSequence();
			var template = new UriTemplate("{bad,later}");

			var act = () => template.Expand(
				("later", (object?)landmine),
				("bad", (object?)"\uD800"));

			act.Should().Throw<FormatException>()
				.WithMessage("*unpaired UTF-16 surrogate*");
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// A failure raised while materializing an earlier varspec's own value stops there
		// too: the read of 'keys' aborts at the null key and 'later' is never begun.
		[Fact]
		public void Expand_ObjectDictionary_NullKeyInSameExpression_PreemptsLaterVarSpec()
		{
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new NullKeyDictionary(),
				["later"] = landmine,
			};
			var template = new UriTemplate("{?keys*,later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(NullKeyMessage("keys"));
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// And the failure that reads nothing at all: a prefix over a composite in the first
		// varspec leaves both its own value and the next varspec's untouched.
		[Fact]
		public void Expand_ObjectDictionary_PrefixViolationInSameExpression_PreemptsLaterVarSpec()
		{
			var prefixed = new ThrowingSequence();
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?>
			{
				["items"] = prefixed,
				["later"] = landmine,
			};
			var template = new UriTemplate("{items:3,later}");

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(PrefixOnCompositeMessage("items"));
			prefixed.EnumerationAttempts.Should().Be(0);
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// An undefined operator belongs to the expression, so it pre-empts every varspec of
		// that expression including the first — the expander resolves its strategy before it
		// looks at a single variable.
		[Fact]
		public void Expand_UndefinedOperator_PreemptsEveryVarSpecOfItsOwnExpression()
		{
			var first = new ThrowingSequence();
			var second = new ThrowingSequence();
			var parser = new MutableTokenParser(
				new UriTemplateExpressionToken(
					(UriTemplateOperator)99,
					new[]
					{
						new UriTemplateVarSpec("items", null, false),
						new UriTemplateVarSpec("later", null, false),
					}));
			var template = new UriTemplate("{items,later}", parser, UriTemplateExpander.Default);
			var vars = new Dictionary<string, object?> { ["items"] = first, ["later"] = second };

			var act = () => template.Expand(vars);

			act.Should().Throw<ArgumentOutOfRangeException>();
			first.EnumerationAttempts.Should().Be(0);
			second.EnumerationAttempts.Should().Be(0);
		}

		// Two varspecs of one expression naming the same variable share the one read, the
		// same way two expressions do.
		[Fact]
		public void Expand_ObjectDictionary_TwoVarSpecsInOneExpression_ReadTheValueOnce()
		{
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?> { ["list"] = oneShot };
			var template = new UriTemplate("{list,list}");

			template.Expand(vars).Should().Be("red,green,red,green");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// ----------------------------------------------------------------
		// Expected messages, quoted from UriTemplateExpander
		// ----------------------------------------------------------------

		private static string UnsupportedTypeMessage(string name, Type type) =>
			$"Variable '{name}' has unsupported type '{type.FullName}'. " +
			"Expected string, IEnumerable<string>, IDictionary<string, string>, or IEnumerable<KeyValuePair<string, string>>.";

		private static string PrefixOnCompositeMessage(string name) =>
			$"Prefix modifier is not applicable to composite values per RFC 6570 (variable '{name}').";

		private static string NullKeyMessage(string name) =>
			$"Variable '{name}' contains a null key. " +
			"Associative array keys must be non-null strings.";

		private static string NullValueMessage(string name, string key) =>
			$"Variable '{name}' contains a null value for key '{key}'. " +
			"Associative array values must be non-null strings.";

		private static string NullElementMessage(string name) =>
			$"Variable '{name}' contains a null element. List elements must be non-null strings.";

		// ----------------------------------------------------------------
		// 2. Null UriTemplateValue entries are undefined, not an exception
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_UriTemplateValueDictionary_NullEntry_TreatedAsUndefined()
		{
			var vars = new Dictionary<string, UriTemplateValue> { ["id"] = null! };
			var template = new UriTemplate("/orders/{id}");

			template.Expand(vars).Should().Be("/orders/");
		}

		[Fact]
		public void Expand_UriTemplateValueDictionary_NullEntry_DoesNotAffectOtherVariables()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["id"] = null!,
				["name"] = UriTemplateValue.From("fred"),
			};
			var template = new UriTemplate("/orders/{id}{?name}");

			template.Expand(vars).Should().Be("/orders/?name=fred");
		}

		// ----------------------------------------------------------------
		// 3. Null string values are undefined (documented behavior)
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_StringDictionary_NullValue_TreatedAsUndefined()
		{
			var vars = new Dictionary<string, string> { ["id"] = null! };
			var template = new UriTemplate("/orders/{id}");

			template.Expand(vars).Should().Be("/orders/");
		}

		[Fact]
		public void Expand_StringTuple_NullValue_TreatedAsUndefined()
		{
			var template = new UriTemplate("/orders/{id}");

			template.Expand(("id", (string)null!)).Should().Be("/orders/");
		}

		// ----------------------------------------------------------------
		// 4. Null tuple keys are reported against 'variables', with the index
		// ----------------------------------------------------------------

		[Fact]
		public void Expand_StringTuple_NullKey_ThrowsArgumentExceptionNamingVariables()
		{
			var template = new UriTemplate("{var}");

			var act = () => template.Expand((null!, "x"));

			act.Should().Throw<ArgumentException>()
				.Which.ParamName.Should().Be("variables");
		}

		[Fact]
		public void Expand_StringTuple_NullKey_MessageNamesOffendingEntryIndex()
		{
			var template = new UriTemplate("{var}");

			var act = () => template.Expand(("var", "x"), (null!, "y"));

			act.Should().Throw<ArgumentException>()
				.WithMessage("*index 1*");
		}

		[Fact]
		public void Expand_ObjectTuple_NullKey_ThrowsArgumentExceptionNamingVariables()
		{
			var template = new UriTemplate("{var}");

			var act = () => template.Expand((null!, (object?)"x"));

			act.Should().Throw<ArgumentException>()
				.Which.ParamName.Should().Be("variables");
		}

		[Fact]
		public void Expand_ObjectTuple_NullKey_MessageNamesOffendingEntryIndex()
		{
			var template = new UriTemplate("{var}");

			var act = () => template.Expand(("var", (object?)"x"), (null!, (object?)"y"));

			act.Should().Throw<ArgumentException>()
				.WithMessage("*index 1*");
		}

		// ----------------------------------------------------------------
		// 5. A parser returning null fails in the constructor
		// ----------------------------------------------------------------

		[Fact]
		public void Constructor_ParserReturnsNull_ThrowsInvalidOperationExceptionNamingParserType()
		{
			var act = () => new UriTemplate("{v}", new NullReturningParser(), UriTemplateExpander.Default);

			act.Should().Throw<InvalidOperationException>()
				.WithMessage("*NullReturningParser*");
		}

		[Fact]
		public void Factory_ParserReturnsNull_ThrowsInvalidOperationExceptionNamingParserType()
		{
			var factory = new UriTemplateFactory(new NullReturningParser(), UriTemplateExpander.Default);

			var act = () => factory.Create("{v}");

			act.Should().Throw<InvalidOperationException>()
				.WithMessage("*NullReturningParser*");
		}

		// ----------------------------------------------------------------
		// 6. The tokens observed at construction are the tokens expanded
		// ----------------------------------------------------------------

		// UriTemplate copies the parser's token list in the constructor. IUriTemplateParser
		// is a public extension point, so that list is caller-owned and may keep changing
		// afterwards: retaining it would let a template expand something other than what it
		// was constructed from. Each varspec's prefix modifier decides whether that
		// reference is legal at all over a composite value, so a mutation between
		// construction and expansion is directly observable.

		// Adding a prefix after construction. Against the retained list the expander would
		// read the mutated varspec and report a prefix violation for a template that was
		// constructed without one. Against the copy the mutation is simply not visible: the
		// template stays "{items}".
		[Fact]
		public void Expand_ParserAddsPrefixAfterConstruction_ExpandsTheTokensObservedAtConstruction()
		{
			var parser = new MutableTokenParser(
				Expression(new UriTemplateVarSpec("items", null, false)));
			var template = new UriTemplate("{items}", parser, UriTemplateExpander.Default);
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?> { ["items"] = oneShot };

			parser.Tokens[0] = Expression(new UriTemplateVarSpec("items", 3, false));

			template.Expand(vars).Should().Be("red,green");
			oneShot.EnumerationCount.Should().Be(1);
		}

		// Removing a prefix after construction. Against the retained list neither mutated
		// reference carries the prefix the constructed template does, so the expansion would
		// succeed — draining the caller's single-pass value twice on the way — where the
		// constructed form fails. Against the copy the template stays "{items:3}", which is
		// a prefix over a composite: reported without reading the value at all.
		[Fact]
		public void Expand_ParserRemovesPrefixAfterConstruction_StillReportsItWithoutEnumerating()
		{
			var parser = new MutableTokenParser(
				Expression(new UriTemplateVarSpec("items", 3, false)));
			var template = new UriTemplate("{items:3}", parser, UriTemplateExpander.Default);
			var oneShot = new SinglePassList("red", "green");
			var vars = new Dictionary<string, object?> { ["items"] = oneShot };

			parser.Tokens.Clear();
			parser.Tokens.Add(Expression(new UriTemplateVarSpec("items", null, false)));
			parser.Tokens.Add(Expression(new UriTemplateVarSpec("items", null, false)));

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(PrefixOnCompositeMessage("items"));
			oneShot.EnumerationCount.Should().Be(0);
		}

		// An expression's operator is a failure the expression owns, and only a custom
		// IUriTemplateParser can produce an undefined one. A preparation pass ahead of the
		// expander put it after the expression's values had been read, so a throwing,
		// blocking, or endless composite the expression named masked it. With nothing
		// running ahead of the expander, the strategy it resolves as its first act is
		// resolved before any of the expression's variables are touched.
		[Fact]
		public void Expand_ParserReturnsUndefinedOperator_IsReportedBeforeAnyValueIsRead()
		{
			var parser = new MutableTokenParser(
				new UriTemplateExpressionToken(
					(UriTemplateOperator)99,
					new[] { new UriTemplateVarSpec("items", null, false) }));
			var template = new UriTemplate("{items}", parser, UriTemplateExpander.Default);
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?> { ["items"] = landmine };

			var act = () => template.Expand(vars);

			act.Should().Throw<ArgumentOutOfRangeException>();
			landmine.EnumerationAttempts.Should().Be(0);
		}

		[Fact]
		public void Expand_ObjectTuple_ParserReturnsUndefinedOperator_IsReportedBeforeAnyValueIsRead()
		{
			var parser = new MutableTokenParser(
				new UriTemplateExpressionToken(
					(UriTemplateOperator)99,
					new[] { new UriTemplateVarSpec("items", null, false) }));
			var template = new UriTemplate("{items}", parser, UriTemplateExpander.Default);
			var landmine = new ThrowingSequence();

			var act = () => template.Expand(("items", (object?)landmine));

			act.Should().Throw<ArgumentOutOfRangeException>();
			landmine.EnumerationAttempts.Should().Be(0);
		}

		// Resolving the operator per expression, rather than in a pass of its own, is what
		// keeps the established rule: the first problem in template order is the one
		// reported. A whole-template operator pass would report the bad operator here even
		// though the template names the unsupported value before it.
		[Fact]
		public void Expand_UndefinedOperatorAfterAnEarlierFailure_StillReportsTheEarlierFailure()
		{
			var parser = new MutableTokenParser(
				Expression(new UriTemplateVarSpec("bad", null, false)),
				new UriTemplateExpressionToken(
					(UriTemplateOperator)99,
					new[] { new UriTemplateVarSpec("items", null, false) }));
			var template = new UriTemplate("{bad}{items}", parser, UriTemplateExpander.Default);
			var vars = new Dictionary<string, object?> { ["bad"] = 42, ["items"] = new ThrowingSequence() };

			var act = () => template.Expand(vars);

			act.Should().Throw<FormatException>()
				.Which.Message.Should().Be(UnsupportedTypeMessage("bad", typeof(int)));
		}

		// And the converse: a bad operator ahead of a later failure still wins, because it
		// is what template order reaches first.
		[Fact]
		public void Expand_UndefinedOperatorBeforeALaterFailure_ReportsTheOperator()
		{
			var parser = new MutableTokenParser(
				new UriTemplateExpressionToken(
					(UriTemplateOperator)99,
					new[] { new UriTemplateVarSpec("items", null, false) }),
				Expression(new UriTemplateVarSpec("bad", null, false)));
			var template = new UriTemplate("{items}{bad}", parser, UriTemplateExpander.Default);
			var landmine = new ThrowingSequence();
			var vars = new Dictionary<string, object?> { ["bad"] = 42, ["items"] = landmine };

			var act = () => template.Expand(vars);

			act.Should().Throw<ArgumentOutOfRangeException>();
			landmine.EnumerationAttempts.Should().Be(0);
		}

		private static UriTemplateExpressionToken Expression(params UriTemplateVarSpec[] variables) =>
			new UriTemplateExpressionToken(UriTemplateOperator.None, variables);

		// ----------------------------------------------------------------
		// Test doubles
		// ----------------------------------------------------------------

		/// <summary>
		/// A genuinely single-pass sequence: the second call to
		/// <see cref="GetEnumerator"/> throws, so any code path that re-enumerates
		/// the caller's value instead of the recorded members fails loudly.
		/// </summary>
		private sealed class SinglePassList : IEnumerable<string>
		{
			private readonly string[] _items;

			internal SinglePassList(params string[] items) => _items = items;

			internal int EnumerationCount { get; private set; }

			public IEnumerator<string> GetEnumerator()
			{
				EnumerationCount++;

				if (EnumerationCount > 1)
				{
					throw new InvalidOperationException("This sequence can only be enumerated once.");
				}

				return ((IEnumerable<string>)_items).GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <summary>
		/// The associative-array counterpart of <see cref="SinglePassList"/>.
		/// </summary>
		private sealed class SinglePassPairs : IEnumerable<KeyValuePair<string, string>>
		{
			private readonly KeyValuePair<string, string>[] _items;

			internal SinglePassPairs(params KeyValuePair<string, string>[] items) => _items = items;

			internal int EnumerationCount { get; private set; }

			public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
			{
				EnumerationCount++;

				if (EnumerationCount > 1)
				{
					throw new InvalidOperationException("This sequence can only be enumerated once.");
				}

				return ((IEnumerable<KeyValuePair<string, string>>)_items).GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <summary>
		/// Yields different contents on each enumeration, standing in for a live query
		/// or a collection mutated between expressions.
		/// </summary>
		private sealed class ChangingList : IEnumerable<string>
		{
			private readonly string[][] _passes;
			private int _pass;

			internal ChangingList(params string[][] passes) => _passes = passes;

			public IEnumerator<string> GetEnumerator()
			{
				var items = _passes[Math.Min(_pass, _passes.Length - 1)];
				_pass++;
				return ((IEnumerable<string>)items).GetEnumerator();
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <summary>
		/// Stands in for a lazy sequence that cannot be enumerated safely — one that
		/// throws, blocks, or never ends. Any attempt to read it is recorded, so a test
		/// can assert the value was left alone entirely.
		/// </summary>
		private sealed class ThrowingSequence : IEnumerable<string>
		{
			internal int EnumerationAttempts { get; private set; }

			public IEnumerator<string> GetEnumerator()
			{
				EnumerationAttempts++;
				throw new InvalidOperationException("This sequence must never be enumerated.");
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <summary>
		/// An <see cref="IDictionary{TKey, TValue}"/> that enumerates a null key, which
		/// <see cref="Dictionary{TKey, TValue}"/> itself cannot hold. Only enumeration is
		/// implemented; the expander needs nothing else.
		/// </summary>
		private sealed class NullKeyDictionary : IDictionary<string, string>
		{
			private readonly KeyValuePair<string, string>[] _entries =
			{
				new KeyValuePair<string, string>("ok", "1"),
				new KeyValuePair<string, string>(null!, "2"),
			};

			public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
				((IEnumerable<KeyValuePair<string, string>>)_entries).GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => _entries.Length;

			public bool IsReadOnly => true;

			public string this[string key]
			{
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public ICollection<string> Keys => throw new NotSupportedException();

			public ICollection<string> Values => throw new NotSupportedException();

			public void Add(string key, string value) => throw new NotSupportedException();

			public void Add(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public void Clear() => throw new NotSupportedException();

			public bool Contains(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool ContainsKey(string key) => throw new NotSupportedException();

			public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => throw new NotSupportedException();

			public bool Remove(string key) => throw new NotSupportedException();

			public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool TryGetValue(string key, out string value) => throw new NotSupportedException();
		}

		/// <summary>
		/// Base for <see cref="IDictionary{TKey, TValue}"/> test doubles that model a
		/// caller-supplied associative array. Only enumeration is meaningful; every
		/// other member is unsupported so that any accidental reliance on it fails
		/// loudly rather than silently.
		/// </summary>
		private abstract class EnumerationOnlyDictionary : IDictionary<string, string>
		{
			public abstract IEnumerator<KeyValuePair<string, string>> GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => throw new NotSupportedException();

			public bool IsReadOnly => true;

			public string this[string key]
			{
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public ICollection<string> Keys => throw new NotSupportedException();

			public ICollection<string> Values => throw new NotSupportedException();

			public void Add(string key, string value) => throw new NotSupportedException();

			public void Add(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public void Clear() => throw new NotSupportedException();

			public bool Contains(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool ContainsKey(string key) => throw new NotSupportedException();

			public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => throw new NotSupportedException();

			public bool Remove(string key) => throw new NotSupportedException();

			public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool TryGetValue(string key, out string value) => throw new NotSupportedException();
		}

		/// <summary>
		/// A genuinely single-pass <see cref="IDictionary{TKey, TValue}"/>: the second
		/// call to <see cref="GetEnumerator"/> throws, so any code path that reads the
		/// caller's dictionary twice fails loudly instead of quietly substituting
		/// <see cref="InvalidOperationException"/> for the documented
		/// <see cref="FormatException"/>.
		/// </summary>
		private class SinglePassDictionary : EnumerationOnlyDictionary
		{
			private readonly KeyValuePair<string, string>[] _entries;

			internal SinglePassDictionary(params KeyValuePair<string, string>[] entries) => _entries = entries;

			internal int EnumerationCount { get; private set; }

			public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
			{
				EnumerationCount++;

				if (EnumerationCount > 1)
				{
					throw new InvalidOperationException("This dictionary can only be enumerated once.");
				}

				return ((IEnumerable<KeyValuePair<string, string>>)_entries).GetEnumerator();
			}
		}

		/// <summary>
		/// A single-pass dictionary whose only enumeration yields a null key.
		/// </summary>
		private sealed class SinglePassNullKeyDictionary : SinglePassDictionary
		{
			internal SinglePassNullKeyDictionary()
				: base(
					new KeyValuePair<string, string>("ok", "1"),
					new KeyValuePair<string, string>(null!, "2"))
			{
			}
		}

		/// <summary>
		/// The associative-array counterpart of <see cref="ThrowingSequence"/>: a
		/// dictionary that cannot be enumerated safely.
		/// </summary>
		private sealed class ThrowingDictionary : EnumerationOnlyDictionary
		{
			internal int EnumerationAttempts { get; private set; }

			public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
			{
				EnumerationAttempts++;
				throw new InvalidOperationException("This dictionary must never be enumerated.");
			}
		}

		/// <summary>
		/// A list whose enumeration cannot safely be continued past the items it yields:
		/// asking for one more element throws. It stands in for a lazy sequence that
		/// throws, blocks, or never ends after the member that should have stopped the
		/// copy, and records whether anything ever read that far.
		/// </summary>
		private sealed class HostileList : IEnumerable<string>
		{
			private readonly string[] _items;

			internal HostileList(params string[] items) => _items = items;

			internal bool ReachedRemainder { get; private set; }

			public IEnumerator<string> GetEnumerator()
			{
				foreach (var item in _items)
				{
					yield return item;
				}

				ReachedRemainder = true;
				throw new InvalidOperationException("The remainder of this sequence must never be read.");
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <summary>
		/// The associative-array counterpart of <see cref="HostileList"/>.
		/// </summary>
		private sealed class HostilePairs : IEnumerable<KeyValuePair<string, string>>
		{
			private readonly KeyValuePair<string, string>[] _entries;

			internal HostilePairs(params KeyValuePair<string, string>[] entries) => _entries = entries;

			internal bool ReachedRemainder { get; private set; }

			public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
			{
				foreach (var entry in _entries)
				{
					yield return entry;
				}

				ReachedRemainder = true;
				throw new InvalidOperationException("The remainder of this sequence must never be read.");
			}

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
		}

		/// <summary>
		/// The <see cref="IDictionary{TKey, TValue}"/> counterpart of
		/// <see cref="HostileList"/>, so the dictionary branch of the snapshot is held to
		/// the same standard as the sequence branches.
		/// </summary>
		private sealed class HostileDictionary : EnumerationOnlyDictionary
		{
			private readonly KeyValuePair<string, string>[] _entries;

			private HostileDictionary(KeyValuePair<string, string>[] entries) => _entries = entries;

			internal static HostileDictionary After(params KeyValuePair<string, string>[] entries) =>
				new HostileDictionary(entries);

			internal bool ReachedRemainder { get; private set; }

			public override IEnumerator<KeyValuePair<string, string>> GetEnumerator()
			{
				foreach (var entry in _entries)
				{
					yield return entry;
				}

				ReachedRemainder = true;
				throw new InvalidOperationException("The remainder of this dictionary must never be read.");
			}
		}

		/// <summary>
		/// A minimal custom <see cref="ISet{T}"/> of pairs, so the snapshot's set branch
		/// is proven to be selected by the interface rather than by
		/// <see cref="HashSet{T}"/> in particular.
		/// <para>
		/// It stores exactly the entries it is given, which also lets it stand in for a set
		/// whose comparer is finer than <see cref="EqualityComparer{T}.Default"/>: it can
		/// hold two entries the default relation would merge.
		/// </para>
		/// </summary>
		private sealed class SingleEntryPairSet : ISet<KeyValuePair<string, string>>
		{
			private readonly KeyValuePair<string, string>[] _entries;

			internal SingleEntryPairSet(params KeyValuePair<string, string>[] entries) => _entries = entries;

			public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
				((IEnumerable<KeyValuePair<string, string>>)_entries).GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => _entries.Length;

			public bool IsReadOnly => true;

			public bool Add(KeyValuePair<string, string> item) => throw new NotSupportedException();

			void ICollection<KeyValuePair<string, string>>.Add(KeyValuePair<string, string> item) =>
				throw new NotSupportedException();

			public void Clear() => throw new NotSupportedException();

			public bool Contains(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => throw new NotSupportedException();

			public void ExceptWith(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public void IntersectWith(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public bool IsProperSubsetOf(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public bool IsProperSupersetOf(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public bool IsSubsetOf(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public bool IsSupersetOf(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public bool Overlaps(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool SetEquals(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public void SymmetricExceptWith(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();

			public void UnionWith(IEnumerable<KeyValuePair<string, string>> other) => throw new NotSupportedException();
		}

		/// <summary>
		/// Captures the value a variable holds by the time expansion begins, which is how
		/// a test can assert what runtime type reached the expander — the type the
		/// expander's associative-array dispatch keys on.
		/// </summary>
		private sealed class RecordingExpander : IUriTemplateExpander
		{
			internal object? Captured { get; private set; }

			public string Expand(UriTemplateExpressionToken expression, IDictionary<string, object?> variables)
			{
				foreach (var varSpec in expression.Variables)
				{
					if (variables.TryGetValue(varSpec.Name, out var value))
					{
						Captured = value;
					}
				}

				return "";
			}
		}

		private sealed class NullReturningParser : IUriTemplateParser
		{
			public IReadOnlyList<UriTemplateToken> Parse(string template) => null!;
		}

		/// <summary>
		/// An <see cref="IDictionary{TKey, TValue}"/> whose key equality is reference
		/// identity rather than ordinal text, so it can legally hold two entries whose keys
		/// read the same. Only enumeration and <see cref="Count"/> are implemented, which is
		/// all the memoizing view and the expander need.
		/// </summary>
		private sealed class ReferenceKeyedDictionary : IDictionary<string, string>
		{
			private readonly List<KeyValuePair<string, string>> _entries = new List<KeyValuePair<string, string>>();

			internal ReferenceKeyedDictionary(params KeyValuePair<string, string>[] entries)
			{
				foreach (var entry in entries)
				{
					// Reference identity on the key, the point of the double: two textually
					// equal but distinct key instances are two entries, not one.
					var existing = _entries.FindIndex(e => ReferenceEquals(e.Key, entry.Key));

					if (existing >= 0)
					{
						_entries[existing] = entry;
					}
					else
					{
						_entries.Add(entry);
					}
				}
			}

			public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _entries.GetEnumerator();

			System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

			public int Count => _entries.Count;

			public bool IsReadOnly => true;

			public string this[string key]
			{
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public ICollection<string> Keys => throw new NotSupportedException();

			public ICollection<string> Values => throw new NotSupportedException();

			public void Add(string key, string value) => throw new NotSupportedException();

			public void Add(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public void Clear() => throw new NotSupportedException();

			public bool Contains(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool ContainsKey(string key) => throw new NotSupportedException();

			public void CopyTo(KeyValuePair<string, string>[] array, int arrayIndex) => throw new NotSupportedException();

			public bool Remove(string key) => throw new NotSupportedException();

			public bool Remove(KeyValuePair<string, string> item) => throw new NotSupportedException();

			public bool TryGetValue(string key, out string value) => throw new NotSupportedException();
		}

		/// <summary>
		/// A hostile-but-legal <see cref="IUriTemplateParser"/>: it hands back a mutable
		/// list and keeps hold of it, so a test can change the "parsed" template after a
		/// <see cref="UriTemplate"/> has already been built from it.
		/// </summary>
		private sealed class MutableTokenParser : IUriTemplateParser
		{
			internal List<UriTemplateToken> Tokens { get; }

			internal MutableTokenParser(params UriTemplateToken[] tokens) =>
				Tokens = new List<UriTemplateToken>(tokens);

			public IReadOnlyList<UriTemplateToken> Parse(string template) => Tokens;
		}
	}
}
