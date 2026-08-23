using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Construction-time validation tests for the token types
	/// (<see cref="UriTemplateExpressionToken"/>, <see cref="UriTemplateVarSpec"/>)
	/// and for the internal operator strategy factory.
	/// </summary>
	public class UriTemplateTypeValidationTests
	{
		// ---------------------------------------------------------------
		// UriTemplateExpressionToken
		// ---------------------------------------------------------------

		[Fact]
		public void ExpressionToken_CopiesVariables_CallerMutationDoesNotAffectToken()
		{
			var variables = new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) };

			var token = new UriTemplateExpressionToken(UriTemplateOperator.None, variables);

			variables.Clear();

			token.Variables.Should().HaveCount(1);
			token.Variables[0].Name.Should().Be("a");
		}

		[Fact]
		public void ExpressionToken_CopiesVariables_CallerAdditionDoesNotAffectToken()
		{
			var variables = new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) };

			var token = new UriTemplateExpressionToken(UriTemplateOperator.None, variables);

			variables.Add(new UriTemplateVarSpec("b", null, false));

			token.Variables.Should().HaveCount(1);
		}

		[Fact]
		public void ExpressionToken_NullVariables_Throws()
		{
			Action act = () => new UriTemplateExpressionToken(UriTemplateOperator.None, null!);

			act.Should().Throw<ArgumentNullException>()
				.Which.ParamName.Should().Be("variables");
		}

		[Fact]
		public void ExpressionToken_EmptyVariables_Throws()
		{
			Action act = () => new UriTemplateExpressionToken(UriTemplateOperator.None, Array.Empty<UriTemplateVarSpec>());

			act.Should().Throw<ArgumentException>()
				.Which.ParamName.Should().Be("variables");
		}

		[Fact]
		public void ExpressionToken_NullVariableElement_Throws()
		{
			var variables = new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false), null! };

			Action act = () => new UriTemplateExpressionToken(UriTemplateOperator.None, variables);

			act.Should().Throw<ArgumentException>()
				.Which.ParamName.Should().Be("variables");
		}

		[Fact]
		public void ExpressionToken_ValidVariables_AreExposedInOrder()
		{
			var variables = new List<UriTemplateVarSpec>
			{
				new UriTemplateVarSpec("a", 3, false),
				new UriTemplateVarSpec("b", null, true),
			};

			var token = new UriTemplateExpressionToken(UriTemplateOperator.Query, variables);

			token.Operator.Should().Be(UriTemplateOperator.Query);
			token.Variables.Should().HaveCount(2);
			token.Variables[0].Should().Be(new UriTemplateVarSpec("a", 3, false));
			token.Variables[1].Should().Be(new UriTemplateVarSpec("b", null, true));
		}

		[Fact]
		public void ExpressionToken_Variables_IsNotACastableMutableArray()
		{
			var token = new UriTemplateExpressionToken(
				UriTemplateOperator.None,
				new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) });

			(token.Variables as UriTemplateVarSpec[]).Should().BeNull();
			token.Variables.Should().BeAssignableTo<ReadOnlyCollection<UriTemplateVarSpec>>();
		}

		[Fact]
		public void ExpressionToken_Variables_IndexerAssignmentThroughIList_Throws()
		{
			var token = new UriTemplateExpressionToken(
				UriTemplateOperator.None,
				new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) });

			var mutable = (IList<UriTemplateVarSpec>)token.Variables;

			Action act = () => mutable[0] = new UriTemplateVarSpec("b", null, false);

			act.Should().Throw<NotSupportedException>();
			token.Variables[0].Name.Should().Be("a");
		}

		[Fact]
		public void ExpressionToken_Variables_NullAssignmentThroughIList_Throws()
		{
			var token = new UriTemplateExpressionToken(
				UriTemplateOperator.None,
				new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) });

			var mutable = (IList<UriTemplateVarSpec>)token.Variables;

			Action act = () => mutable[0] = null!;

			act.Should().Throw<NotSupportedException>();
			token.Variables[0].Should().NotBeNull();
		}

		[Fact]
		public void ExpressionToken_Variables_AddThroughIList_Throws()
		{
			var token = new UriTemplateExpressionToken(
				UriTemplateOperator.None,
				new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) });

			var mutable = (IList<UriTemplateVarSpec>)token.Variables;

			mutable.IsReadOnly.Should().BeTrue();

			Action act = () => mutable.Add(new UriTemplateVarSpec("b", null, false));

			act.Should().Throw<NotSupportedException>();
			token.Variables.Should().HaveCount(1);
		}

		[Fact]
		public void ExpressionToken_Variables_ClearThroughIList_Throws()
		{
			var token = new UriTemplateExpressionToken(
				UriTemplateOperator.None,
				new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) });

			var mutable = (IList<UriTemplateVarSpec>)token.Variables;

			Action act = () => mutable.Clear();

			act.Should().Throw<NotSupportedException>();
			token.Variables.Should().HaveCount(1);
		}

		[Fact]
		public void ExpressionToken_Variables_RemoveAndInsertThroughIList_Throw()
		{
			var token = new UriTemplateExpressionToken(
				UriTemplateOperator.None,
				new List<UriTemplateVarSpec> { new UriTemplateVarSpec("a", null, false) });

			var mutable = (IList<UriTemplateVarSpec>)token.Variables;
			var existing = token.Variables[0];

			Action insert = () => mutable.Insert(0, new UriTemplateVarSpec("b", null, false));
			Action removeAt = () => mutable.RemoveAt(0);
			Action remove = () => mutable.Remove(existing);

			insert.Should().Throw<NotSupportedException>();
			removeAt.Should().Throw<NotSupportedException>();
			remove.Should().Throw<NotSupportedException>();
			token.Variables.Should().HaveCount(1);
		}

		// ---------------------------------------------------------------
		// UriTemplateVarSpec
		// ---------------------------------------------------------------

		[Fact]
		public void VarSpec_NullName_Throws()
		{
			Action act = () => new UriTemplateVarSpec(null!, null, false);

			act.Should().Throw<ArgumentNullException>()
				.Which.ParamName.Should().Be("Name");
		}

		[Fact]
		public void VarSpec_EmptyName_Throws()
		{
			Action act = () => new UriTemplateVarSpec("", null, false);

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("Name");
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(10000)]
		public void VarSpec_PrefixLengthOutOfRange_Throws(int prefixLength)
		{
			Action act = () => new UriTemplateVarSpec("var", prefixLength, false);

			act.Should().Throw<ArgumentOutOfRangeException>()
				.Which.ParamName.Should().Be("PrefixLength");
		}

		[Theory]
		[InlineData(1)]
		[InlineData(3)]
		[InlineData(9999)]
		public void VarSpec_PrefixLengthInRange_IsAccepted(int prefixLength)
		{
			var spec = new UriTemplateVarSpec("var", prefixLength, false);

			spec.Name.Should().Be("var");
			spec.PrefixLength.Should().Be(prefixLength);
			spec.Explode.Should().BeFalse();
		}

		[Fact]
		public void VarSpec_PrefixLengthWithExplode_Throws()
		{
			Action act = () => new UriTemplateVarSpec("var", 3, true);

			act.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void VarSpec_ExplodeWithoutPrefixLength_IsAccepted()
		{
			var spec = new UriTemplateVarSpec("var", null, true);

			spec.PrefixLength.Should().BeNull();
			spec.Explode.Should().BeTrue();
		}

		[Fact]
		public void VarSpec_PlainName_IsAccepted()
		{
			var spec = new UriTemplateVarSpec("var", null, false);

			spec.Name.Should().Be("var");
			spec.PrefixLength.Should().BeNull();
			spec.Explode.Should().BeFalse();
		}

		// ---------------------------------------------------------------
		// UriTemplateVarSpec - object-initializer / 'with' construction paths
		// ---------------------------------------------------------------

		[Fact]
		public void VarSpec_ObjectInitializer_NullName_Throws()
		{
			Action act = () => new UriTemplateVarSpec("var", null, false) { Name = null! };

			act.Should().Throw<ArgumentNullException>()
				.Which.ParamName.Should().Be("Name");
		}

		[Fact]
		public void VarSpec_ObjectInitializer_EmptyName_Throws()
		{
			Action act = () => new UriTemplateVarSpec("var", null, false) { Name = "" };

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("Name");
		}

		[Fact]
		public void VarSpec_WithExpression_EmptyName_Throws()
		{
			var spec = new UriTemplateVarSpec("var", null, false);

			Action act = () => _ = spec with { Name = "" };

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("Name");
		}

		[Fact]
		public void VarSpec_WithExpression_NullName_Throws()
		{
			var spec = new UriTemplateVarSpec("var", null, false);

			Action act = () => _ = spec with { Name = null! };

			act.Should().Throw<ArgumentNullException>()
				.Which.ParamName.Should().Be("Name");
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(10000)]
		public void VarSpec_ObjectInitializer_PrefixLengthOutOfRange_Throws(int prefixLength)
		{
			Action act = () => new UriTemplateVarSpec("var", null, false) { PrefixLength = prefixLength };

			act.Should().Throw<ArgumentOutOfRangeException>()
				.Which.ParamName.Should().Be("PrefixLength");
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(10000)]
		public void VarSpec_WithExpression_PrefixLengthOutOfRange_Throws(int prefixLength)
		{
			var spec = new UriTemplateVarSpec("var", null, false);

			Action act = () => _ = spec with { PrefixLength = prefixLength };

			act.Should().Throw<ArgumentOutOfRangeException>()
				.Which.ParamName.Should().Be("PrefixLength");
		}

		[Fact]
		public void VarSpec_WithExpression_ExplodeOnPrefixedSpec_Throws()
		{
			var spec = new UriTemplateVarSpec("var", 3, false);

			Action act = () => _ = spec with { Explode = true };

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("Explode");
		}

		[Fact]
		public void VarSpec_WithExpression_PrefixLengthOnExplodedSpec_Throws()
		{
			var spec = new UriTemplateVarSpec("var", null, true);

			Action act = () => _ = spec with { PrefixLength = 3 };

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("PrefixLength");
		}

		[Fact]
		public void VarSpec_ObjectInitializer_PrefixLengthThenExplode_Throws()
		{
			Action act = () => new UriTemplateVarSpec("var", null, false)
			{
				PrefixLength = 3,
				Explode = true,
			};

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("Explode");
		}

		[Fact]
		public void VarSpec_ObjectInitializer_ExplodeThenPrefixLength_Throws()
		{
			Action act = () => new UriTemplateVarSpec("var", null, false)
			{
				Explode = true,
				PrefixLength = 3,
			};

			act.Should().ThrowExactly<ArgumentException>()
				.Which.ParamName.Should().Be("PrefixLength");
		}

		[Fact]
		public void VarSpec_ObjectInitializer_ValidValues_AreAccepted()
		{
			var spec = new UriTemplateVarSpec("var", null, false)
			{
				Name = "other",
				PrefixLength = 4,
			};

			spec.Name.Should().Be("other");
			spec.PrefixLength.Should().Be(4);
			spec.Explode.Should().BeFalse();
		}

		[Fact]
		public void VarSpec_WithExpression_ValidValues_AreAccepted()
		{
			var spec = new UriTemplateVarSpec("var", null, false);

			var prefixed = spec with { PrefixLength = 9999 };
			var exploded = spec with { Explode = true };

			prefixed.PrefixLength.Should().Be(9999);
			prefixed.Explode.Should().BeFalse();
			exploded.PrefixLength.Should().BeNull();
			exploded.Explode.Should().BeTrue();
		}

		[Fact]
		public void VarSpec_WithExpression_ClearingPrefixLengthBeforeSettingExplode_IsAccepted()
		{
			var spec = new UriTemplateVarSpec("var", 3, false);

			var exploded = spec with { PrefixLength = null, Explode = true };

			exploded.PrefixLength.Should().BeNull();
			exploded.Explode.Should().BeTrue();
		}

		[Fact]
		public void VarSpec_WithExpression_ClearingExplodeBeforeSettingPrefixLength_IsAccepted()
		{
			var spec = new UriTemplateVarSpec("var", null, true);

			var prefixed = spec with { Explode = false, PrefixLength = 3 };

			prefixed.PrefixLength.Should().Be(3);
			prefixed.Explode.Should().BeFalse();
		}

		[Fact]
		public void VarSpec_WithExpression_UnrelatedChange_PreservesModifiers()
		{
			var prefixed = new UriTemplateVarSpec("var", 3, false) with { Name = "other" };
			var exploded = new UriTemplateVarSpec("var", null, true) with { Name = "other" };

			prefixed.Should().Be(new UriTemplateVarSpec("other", 3, false));
			exploded.Should().Be(new UriTemplateVarSpec("other", null, true));
		}

		[Fact]
		public void VarSpec_EqualityIsUnchanged()
		{
			new UriTemplateVarSpec("var", 3, false)
				.Should().Be(new UriTemplateVarSpec("var", 3, false));

			new UriTemplateVarSpec("var", 3, false)
				.Should().NotBe(new UriTemplateVarSpec("var", 4, false));
		}

		// ---------------------------------------------------------------
		// OperatorStrategyFactory
		// ---------------------------------------------------------------

		[Fact]
		public void OperatorStrategyFactory_UnknownOperator_ThrowsArgumentOutOfRange()
		{
			Action act = () => OperatorStrategyFactory.For((UriTemplateOperator)99);

			act.Should().Throw<ArgumentOutOfRangeException>()
				.Which.ParamName.Should().Be("op");
		}

		[Fact]
		public void OperatorStrategyFactory_UnknownOperator_ExceptionCarriesTheOperator()
		{
			Action act = () => OperatorStrategyFactory.For((UriTemplateOperator)99);

			act.Should().Throw<ArgumentOutOfRangeException>()
				.Which.ActualValue.Should().Be((UriTemplateOperator)99);
		}

		[Theory]
		[InlineData(UriTemplateOperator.None)]
		[InlineData(UriTemplateOperator.Plus)]
		[InlineData(UriTemplateOperator.Hash)]
		[InlineData(UriTemplateOperator.Dot)]
		[InlineData(UriTemplateOperator.Slash)]
		[InlineData(UriTemplateOperator.Semicolon)]
		[InlineData(UriTemplateOperator.Query)]
		[InlineData(UriTemplateOperator.Ampersand)]
		public void OperatorStrategyFactory_KnownOperators_ResolveToStrategy(UriTemplateOperator op)
		{
			OperatorStrategyFactory.For(op).Should().NotBeNull();
		}
	}
}
