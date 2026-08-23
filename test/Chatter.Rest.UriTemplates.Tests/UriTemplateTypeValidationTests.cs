using System;
using System.Collections.Generic;
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
