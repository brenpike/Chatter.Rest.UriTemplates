using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Argument-contract tests for <see cref="UriTemplate"/>'s public overloads:
	/// snapshotting of caller-supplied sequences, consistent null handling, explicit
	/// null-key reporting, and validation of a custom parser's result.
	/// </summary>
	public class UriTemplateArgumentContractTests
	{
		// ----------------------------------------------------------------
		// 1. Caller-supplied enumerables are materialized once
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
		// Test doubles
		// ----------------------------------------------------------------

		/// <summary>
		/// A genuinely single-pass sequence: the second call to
		/// <see cref="GetEnumerator"/> throws, so any code path that re-enumerates
		/// the caller's value instead of an owned snapshot fails loudly.
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

		private sealed class NullReturningParser : IUriTemplateParser
		{
			public IReadOnlyList<UriTemplateToken> Parse(string template) => null!;
		}
	}
}
