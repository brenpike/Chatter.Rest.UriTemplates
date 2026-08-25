using System.Collections.Frozen;
using System.Collections.ObjectModel;
using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Associative-array expansion tests covering the determinism guarantee made by
	/// <c>UriTemplateExpander.ExpandAssociativeArray</c>: keyed and set containers are
	/// canonicalized while every other pair sequence is expanded in its own enumeration
	/// order. See "Associative-array pair order" in <c>docs/usage.md</c>.
	///
	/// Empty member names are legal: RFC 6570 §2.3 models associative-array members as
	/// (name, value) string pairs and treats only a zero-member composite as undefined.
	/// Null keys and null values remain rejected as defects.
	///
	/// References: GitHub issue #22, docs/test-plan.md §6.3, RFC 6570 §2.3, §3.2.
	/// </summary>
	public class UriTemplateAssociativeArrayTests
	{
		// ----------------------------------------------------------------
		// 1. Keyed containers — canonical (ordinal key) ordering
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
		// 2. Pair sequences — supplied order preserved
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
		// 3. Empty member names are preserved (RFC 6570 §2.3)
		// ----------------------------------------------------------------

		// RFC 6570 §2.3 places no constraint on associative-array member names; the
		// varchar grammar constrains template variable names, not member names. An
		// empty name therefore expands like any other.
		[Fact]
		public void KeyValuePairSequence_EmptyKey_IsPreserved()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new List<KeyValuePair<string, string>>
				{
					new("", "v"),
				},
			};

			new UriTemplate("{keys}").Expand(vars).Should().Be(",v");
			new UriTemplate("{keys*}").Expand(vars).Should().Be("=v");
			new UriTemplate("{?keys*}").Expand(vars).Should().Be("?=v");
			new UriTemplate("{?keys}").Expand(vars).Should().Be("?keys=,v");
		}

		[Fact]
		public void Dictionary_EmptyKey_IsPreserved()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new Dictionary<string, string>
				{
					[""] = "",
					["x"] = "1",
				},
			};

			// The empty key sorts first under ordinal comparison.
			new UriTemplate("{?keys*}").Expand(vars).Should().Be("?=&x=1");
		}

		[Fact]
		public void Dictionary_EmptyKey_NonExplode_IsPreserved()
		{
			var vars = new Dictionary<string, object?>
			{
				["keys"] = new Dictionary<string, string>
				{
					[""] = "v",
				},
			};

			new UriTemplate("{?keys}").Expand(vars).Should().Be("?keys=,v");
		}

		// ----------------------------------------------------------------
		// 4. Value validation
		// ----------------------------------------------------------------

		// Empty values remain legal.
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

		// ----------------------------------------------------------------
		// 5. Container-shape dispatch — keyed/set containers vs pair sequences
		// ----------------------------------------------------------------

		private static readonly KeyValuePair<string, string>[] ReverseOrdinalPairs =
		{
			new("z", "1"),
			new("m", "2"),
			new("a", "3"),
		};

		// An array is a pair sequence, so its order is the caller's and is preserved.
		[Fact]
		public void Array_IsTreatedAsOrdered()
		{
			var vars = new Dictionary<string, object?> { ["d"] = ReverseOrdinalPairs };
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// ReadOnlyCollection<KVP> is a pair sequence too; same rule.
		[Fact]
		public void ReadOnlyCollection_IsTreatedAsOrdered()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new ReadOnlyCollection<KeyValuePair<string, string>>(
					new List<KeyValuePair<string, string>>(ReverseOrdinalPairs)),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// A Queue<KVP> is not index-addressable, but it is unambiguously ordered: the
		// caller chose the enqueue order and it enumerates front-to-back. Sorting it
		// would silently discard a caller decision, so the supplied order is kept.
		[Fact]
		public void QueueOfPairs_PreservesSuppliedOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new Queue<KeyValuePair<string, string>>(ReverseOrdinalPairs),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// LinkedList<KVP> is likewise ordered without being a list.
		[Fact]
		public void LinkedListOfPairs_PreservesSuppliedOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new LinkedList<KeyValuePair<string, string>>(ReverseOrdinalPairs),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// An iterator method yields pairs in a caller-written order. Nothing about its
		// type says so, which is exactly why the contract defers to the caller here.
		private static IEnumerable<KeyValuePair<string, string>> YieldReverseOrdinalPairs()
		{
			yield return new KeyValuePair<string, string>("z", "1");
			yield return new KeyValuePair<string, string>("m", "2");
			yield return new KeyValuePair<string, string>("a", "3");
		}

		[Fact]
		public void YieldIterator_PreservesSuppliedOrder()
		{
			var vars = new Dictionary<string, object?> { ["d"] = YieldReverseOrdinalPairs() };
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// An order-preserving LINQ pipeline is a sequence, not an unordered container;
		// its projection order is the caller's and survives expansion.
		[Fact]
		public void LinqIterator_PreservesSuppliedOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = ReverseOrdinalPairs.Select(pair => pair),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?z=1&m=2&a=3");
		}

		// A sequence deliberately sorted by the caller is likewise left alone — the
		// result matches the canonical order here only because the caller asked for it.
		[Fact]
		public void LinqOrderByPipeline_PreservesItsOwnOrder()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = ReverseOrdinalPairs.OrderByDescending(pair => pair.Value, StringComparer.Ordinal),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?a=3&m=2&z=1");
		}

		// A Dictionary exposes no API for ordering its pairs, so it is canonicalized.
		[Fact]
		public void Dictionary_IsCanonicalized()
		{
			var dictionary = new Dictionary<string, string>();
			foreach (var pair in ReverseOrdinalPairs)
			{
				dictionary[pair.Key] = pair.Value;
			}

			var vars = new Dictionary<string, object?> { ["d"] = dictionary };
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?a=3&m=2&z=1");
		}

		// A HashSet of pairs models membership only — its enumeration order can change
		// after remove/reinsert or between processes — so it is canonicalized. It
		// reaches that branch as ISet<KeyValuePair<string, string>>, not as a dictionary.
		[Fact]
		public void HashSetOfPairs_IsCanonicalized()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new HashSet<KeyValuePair<string, string>>(ReverseOrdinalPairs),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?a=3&m=2&z=1");
		}

		// FrozenSet<KVP> implements ISet<KVP> without implementing IDictionary, which
		// pins that the set test — not the dictionary test — is what catches it.
		[Fact]
		public void FrozenSetOfPairs_IsCanonicalized()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = ReverseOrdinalPairs.ToFrozenSet(),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?a=3&m=2&z=1");
		}

		// FrozenDictionary implements IDictionary<string, string>, so it is caught by
		// the dictionary test and canonicalized rather than enumerated in whatever
		// bucket order the frozen layout happens to produce.
		[Fact]
		public void FrozenDictionary_IsCanonicalized()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new Dictionary<string, string>
				{
					["z"] = "1",
					["m"] = "2",
					["a"] = "3",
				}.ToFrozenDictionary(),
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?a=3&m=2&z=1");
		}

		// A set may still hold repeated keys. Ordinal key comparison alone would leave
		// those pairs tied under an unstable sort, so the value is compared as a
		// tie-break to keep the canonical order total.
		[Fact]
		public void UnorderedContainer_DuplicateKeys_AreOrderedByKeyThenValue()
		{
			var vars = new Dictionary<string, object?>
			{
				["d"] = new HashSet<KeyValuePair<string, string>>
				{
					new("z", "3"),
					new("a", "2"),
					new("z", "1"),
				},
			};
			new UriTemplate("{?d*}").Expand(vars).Should().Be("?a=2&z=1&z=3");
		}

		// Two logically identical sets built in different insertion orders must expand
		// identically — the determinism guarantee itself, extended past IDictionary.
		[Fact]
		public void UnorderedContainers_BuiltDifferently_ExpandIdentically()
		{
			var forward = new HashSet<KeyValuePair<string, string>>
			{
				new("a", "3"),
				new("m", "2"),
				new("z", "1"),
			};
			var reverse = new HashSet<KeyValuePair<string, string>>(ReverseOrdinalPairs);

			var template = new UriTemplate("{?d*}");
			var fromForward = template.Expand(new Dictionary<string, object?> { ["d"] = forward });
			var fromReverse = template.Expand(new Dictionary<string, object?> { ["d"] = reverse });

			fromForward.Should().Be(fromReverse);
			fromForward.Should().Be("?a=3&m=2&z=1");
		}
	}
}
