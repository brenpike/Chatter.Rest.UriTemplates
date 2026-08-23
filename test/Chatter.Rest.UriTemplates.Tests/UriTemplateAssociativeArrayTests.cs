using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Associative-array expansion tests covering the two determinism/validation
	/// guarantees made by <c>UriTemplateExpander.ExpandAssociativeArray</c>:
	///
	/// 1. Pair ordering is deterministic. Values supplied as an ordered sequence
	///    (<see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/>,
	///    e.g. <see cref="List{T}"/>) keep their enumeration order. Values supplied
	///    as an <see cref="IDictionary{TKey, TValue}"/> — a contract with no defined
	///    enumeration order — are ordered by ordinal key comparison.
	/// 2. Empty-string keys are rejected with <see cref="FormatException"/>, matching
	///    the existing null-key rule.
	///
	/// References: GitHub issue #22, docs/test-plan.md §6.3, RFC 6570 §3.2.
	/// </summary>
	public class UriTemplateAssociativeArrayTests
	{
		// ----------------------------------------------------------------
		// 1. IDictionary inputs — ordinal key ordering
		// ----------------------------------------------------------------

		/// <summary>
		/// Builds the exact map from issue #22: insert z, insert a, remove z,
		/// insert m, insert z. Dictionary reuses the slot freed by the removal,
		/// so its own enumeration order is z-slot-last-written-first and diverges
		/// from insertion order.
		/// </summary>
		private static Dictionary<string, string> MakeSlotReusedDictionary()
		{
			var dict = new Dictionary<string, string>
			{
				["z"] = "1",
				["a"] = "2",
			};
			dict.Remove("z");
			dict["m"] = "3";
			dict["z"] = "4";
			return dict;
		}

		// Repro from issue #22: hash-order enumeration produced "?m=3&a=2&z=4".
		[Fact]
		public void Dictionary_WithReusedSlot_ExplodesInOrdinalKeyOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = MakeSlotReusedDictionary(),
			};
			var template = new UriTemplate("{?d*}");
			template.Expand(vars).Should().Be("?a=2&m=3&z=4");
		}

		// Non-explode flattening uses the same ordering.
		[Fact]
		public void Dictionary_WithReusedSlot_NonExplodeUsesOrdinalKeyOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = MakeSlotReusedDictionary(),
			};
			var template = new UriTemplate("{?d}");
			template.Expand(vars).Should().Be("?d=a,2,m,3,z,4");
		}

		// Two logically identical maps must expand to the same URI regardless of
		// how they were built. This is the determinism guarantee itself.
		[Fact]
		public void Dictionary_LogicallyIdenticalMaps_ExpandIdentically()
		{
			var built = MakeSlotReusedDictionary();
			var literal = new Dictionary<string, string>
			{
				["a"] = "2",
				["m"] = "3",
				["z"] = "4",
			};

			var template = new UriTemplate("{?d*}");
			var fromBuilt = template.Expand(new Dictionary<string, object?> { ["d"] = built });
			var fromLiteral = template.Expand(new Dictionary<string, object?> { ["d"] = literal });

			fromBuilt.Should().Be(fromLiteral);
			fromBuilt.Should().Be("?a=2&m=3&z=4");
		}

		// Ordering is ordinal (code-unit), not culture-aware: uppercase letters
		// sort before '_' which sorts before lowercase letters.
		[Fact]
		public void Dictionary_OrdersKeysOrdinally_NotByCulture()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new Dictionary<string, string>
				{
					["a"] = "1",
					["Z"] = "2",
					["_"] = "3",
					["B"] = "4",
				},
			};
			var template = new UriTemplate("{?d*}");
			template.Expand(vars).Should().Be("?B=4&Z=2&_=3&a=1");
		}

		// Ordering is applied before encoding, so it is stable regardless of the
		// operator's encoding rules.
		[Fact]
		public void Dictionary_OrdinalOrdering_AppliesToEveryOperator()
		{
			var dict = new Dictionary<string, string>
			{
				["z"] = "1",
				["a"] = "2",
			};
			var vars = new Dictionary<string, object?> { ["d"] = dict };

			new UriTemplate("{d*}").Expand(vars).Should().Be("a=2,z=1");
			new UriTemplate("{/d*}").Expand(vars).Should().Be("/a=2/z=1");
			new UriTemplate("{;d*}").Expand(vars).Should().Be(";a=2;z=1");
			new UriTemplate("{&d*}").Expand(vars).Should().Be("&a=2&z=1");
			new UriTemplate("{#d}").Expand(vars).Should().Be("#a,2,z,1");
		}

		// The UriTemplateValue.From(IDictionary<,>) path materializes a
		// Dictionary internally, so it is ordered by the same rule.
		[Fact]
		public void DictionaryValue_OrdersKeysOrdinally()
		{
			var vars = new Dictionary<string, UriTemplateValue>
			{
				["keys"] = UriTemplateValue.From(new Dictionary<string, string>
				{
					["semi"] = ";",
					["dot"] = ".",
					["comma"] = ",",
				}),
			};
			var template = new UriTemplate("{?keys*}");
			template.Expand(vars).Should().Be("?comma=%2C&dot=.&semi=%3B");
		}

		// ----------------------------------------------------------------
		// 2. Ordered sequence inputs — supplied order preserved
		// ----------------------------------------------------------------

		// A List<KeyValuePair<,>> has a defined enumeration order, so it is kept
		// as-is. This is what the RFC 6570 §3.2.1 example set relies on.
		[Fact]
		public void KeyValuePairSequence_PreservesSuppliedOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("semi", ";"),
					new("dot", "."),
					new("comma", ","),
				},
			};

			new UriTemplate("{?keys*}").Expand(vars).Should().Be("?semi=%3B&dot=.&comma=%2C");
			new UriTemplate("{keys}").Expand(vars).Should().Be("semi,%3B,dot,.,comma,%2C");
		}

		// Reverse-ordinal input order must survive untouched — proof that the
		// sequence branch does not sort.
		[Fact]
		public void KeyValuePairSequence_ReverseOrdinalOrder_IsNotSorted()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new List<KeyValuePair<string, string>>
				{
					new("z", "1"),
					new("m", "2"),
					new("a", "3"),
				},
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// A sequence may legitimately repeat a key; ordering must not collapse or
		// reorder those entries.
		[Fact]
		public void KeyValuePairSequence_DuplicateKeys_ArePreservedInOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new List<KeyValuePair<string, string>>
				{
					new("z", "1"),
					new("a", "2"),
					new("z", "3"),
				},
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&a=2&z=3");
		}

		// ----------------------------------------------------------------
		// 3. Empty-string key rejection
		// ----------------------------------------------------------------

		[Fact]
		public void Dictionary_EmptyKey_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new Dictionary<string, string>
				{
					[""] = "",
					["x"] = "1",
				},
			};
			var template = new UriTemplate("{?keys*}");

			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.WithMessage("*keys*empty key*");
		}

		[Fact]
		public void Dictionary_EmptyKey_NonExplode_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new Dictionary<string, string>
				{
					[""] = "v",
				},
			};
			var template = new UriTemplate("{?keys}");

			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>();
		}

		[Fact]
		public void KeyValuePairSequence_EmptyKey_ThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("", "1"),
				},
			};
			var template = new UriTemplate("{keys*}");

			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>();
		}

		// Empty *values* remain legal — only empty keys are rejected.
		[Fact]
		public void EmptyValue_IsStillAccepted()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("x", ""),
					new("y", "1"),
				},
			};
			new UriTemplate("{?keys*}").Expand(vars).Should().Be("?x=&y=1");
		}

		// Existing null-key rule is unchanged.
		[Fact]
		public void KeyValuePairSequence_NullKey_StillThrowsFormatException()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new(null!, "1"),
				},
			};
			var template = new UriTemplate("{keys*}");

			Action act = () => template.Expand(vars);
			act.Should().Throw<FormatException>()
				.WithMessage("*null key*");
		}
	}
}
