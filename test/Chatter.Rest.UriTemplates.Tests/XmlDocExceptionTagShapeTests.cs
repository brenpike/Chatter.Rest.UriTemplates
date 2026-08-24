using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
	/// <summary>
	/// Mechanically enforces the exception-tag shape convention of docs/development.md §7:
	/// every &lt;exception&gt; tag in the core assembly's generated XML documentation carries
	/// at most one sentence (a semicolon-joined clause is one sentence), optionally followed
	/// by one trailing pointer sentence that is exactly: See "…" in docs/usage.md. That
	/// pointer sentence ends at the path — prose appended after it is a violation. Behavioral
	/// detail beyond that shape belongs in the canonical docs/usage.md anchor, not in the tag.
	/// </summary>
	public class XmlDocExceptionTagShapeTests
	{
		private static readonly Regex UsagePointerPattern = new(@"^[Ss]ee ""[^""]+"" in docs/usage\.md\.?$", RegexOptions.Compiled);
		private static readonly Regex AbbreviationPattern = new(@"\b(?:[eE]\.g|[iI]\.e)\.", RegexOptions.Compiled);
		private static readonly Regex DottedNumberPattern = new(@"(?<=\d)\.(?=\d)", RegexOptions.Compiled);
		private static readonly Regex SentenceBoundaryPattern = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
		private static readonly Regex WhitespaceRunPattern = new(@"\s+", RegexOptions.Compiled);

		[Fact]
		public void CoreXmlDoc_EveryExceptionTag_HasAtMostOneSentence()
		{
			var docPath = Path.ChangeExtension(typeof(UriTemplate).Assembly.Location, ".xml");

			File.Exists(docPath).Should().BeTrue(
				"the core assembly's XML documentation file must be copied next to the test assembly, expected at '" + docPath + "'");

			var exceptionTags = XDocument.Load(docPath).Descendants("exception").ToList();

			exceptionTags.Should().NotBeEmpty(
				"the core assembly documents its exception contracts, so finding none means the documentation file did not load correctly");

			var violations = exceptionTags
				.Where(tag => !HasCompliantSentenceShape(tag))
				.Select(DescribeViolation)
				.ToList();

			violations.Should().BeEmpty(
				"every exception tag must carry at most one sentence plus an optional trailing docs/usage.md pointer; " +
				"shared behavioral detail belongs in the canonical docs/usage.md anchor");
		}

		[Theory]
		[InlineData(
			"<exception cref=\"T:System.FormatException\">Thrown when a string held by a value contains an unpaired " +
			"UTF-16 surrogate and cannot be percent-encoded; unsupported types and null members are unreachable " +
			"through this overload, because the factories validate their contents at construction.</exception>")]
		[InlineData(
			"<exception cref=\"T:System.FormatException\">Thrown when a prefix modifier (e.g. <c>{var:3}</c>) is " +
			"applied to a composite value, i.e. a list or associative array, per RFC 6570 §3.2.1.</exception>")]
		[InlineData(
			"<exception cref=\"T:System.NotSupportedException\">Thrown when an expression starts with an operator " +
			"RFC 6570 §2.2 reserves for future use. See \"Constructor exceptions\" in <c>docs/usage.md</c>.</exception>")]
		[InlineData(
			"<exception cref=\"T:System.FormatException\">Thrown when the template is malformed; see " +
			"\"Constructor exceptions\" in <c>docs/usage.md</c> for the enumeration of malformed forms.</exception>")]
		[InlineData(
			"<exception cref=\"T:System.ArgumentNullException\">Thrown when <paramref name=\"template\"/> is null.</exception>")]
		public void HasCompliantSentenceShape_SingleSentenceIdioms_AreAccepted(string fragment)
		{
			HasCompliantSentenceShape(XElement.Parse(fragment)).Should().BeTrue(
				"semicolon-joined clauses, e.g./i.e. abbreviations, dotted section marks, and a trailing " +
				"docs/usage.md pointer are all part of the compliant single-sentence shape");
		}

		[Theory]
		[InlineData(
			"<exception cref=\"T:System.FormatException\">Thrown when the template is malformed. The parser " +
			"rejects nested braces and reports the character offset.</exception>")]
		[InlineData(
			"<exception cref=\"T:System.FormatException\">Thrown when the value is invalid. It is rejected before " +
			"expansion begins. See \"Constructor exceptions\" in <c>docs/usage.md</c>.</exception>")]
		public void HasCompliantSentenceShape_MultiSentenceTag_IsRejected(string fragment)
		{
			HasCompliantSentenceShape(XElement.Parse(fragment)).Should().BeFalse(
				"a tag carrying more than one sentence of behavioral detail must fail the shape check, " +
				"otherwise the check cannot enforce the convention");
		}

		[Theory]
		[InlineData(
			"<exception cref=\"T:System.ArgumentException\">Thrown when an entry has a null key. See " +
			"\"Variable materialization\" in <c>docs/usage.md</c> and the message names the entry " +
			"index.</exception>")]
		[InlineData(
			"<exception cref=\"T:System.FormatException\">Thrown when the template is malformed. See " +
			"\"Constructor exceptions\" in <c>docs/usage.md</c> for the enumeration of malformed " +
			"forms.</exception>")]
		public void HasCompliantSentenceShape_ProseTrailingThePointerPath_IsRejected(string fragment)
		{
			HasCompliantSentenceShape(XElement.Parse(fragment)).Should().BeFalse(
				"the trailing pointer sentence must end at the docs/usage.md path; behavioral detail " +
				"appended after the path would otherwise sail through a start-anchored pattern");
		}

		private static bool HasCompliantSentenceShape(XElement exceptionTag)
		{
			var sentences = SplitSentences(NormalizeTagText(exceptionTag));

			if (sentences.Count <= 1)
			{
				return true;
			}

			return sentences.Count == 2 && UsagePointerPattern.IsMatch(sentences[1]);
		}

		private static IReadOnlyList<string> SplitSentences(string text)
		{
			var masked = AbbreviationPattern.Replace(text, match => match.Value.Replace(".", string.Empty));
			masked = DottedNumberPattern.Replace(masked, string.Empty);

			return SentenceBoundaryPattern.Split(masked)
				.Select(sentence => sentence.Trim())
				.Where(sentence => sentence.Length > 0)
				.ToList();
		}

		private static string NormalizeTagText(XElement exceptionTag)
		{
			var builder = new StringBuilder();
			AppendFlattenedText(exceptionTag, builder);
			return WhitespaceRunPattern.Replace(builder.ToString(), " ").Trim();
		}

		private static void AppendFlattenedText(XElement element, StringBuilder builder)
		{
			foreach (var node in element.Nodes())
			{
				if (node is XText text)
				{
					builder.Append(text.Value);
				}
				else if (node is XElement child)
				{
					if (child.Nodes().Any())
					{
						AppendFlattenedText(child, builder);
					}
					else
					{
						// INVARIANT: self-closing markup (<see cref="…"/>, <paramref name="…"/>) is replaced
						// with a dot-free token, because cref values carry dots that would otherwise read
						// as sentence terminators.
						builder.Append("ref");
					}
				}
			}
		}

		private static string DescribeViolation(XElement exceptionTag)
		{
			var memberName = exceptionTag.Parent?.Attribute("name")?.Value ?? "(unknown member)";
			return memberName + ": " + NormalizeTagText(exceptionTag);
		}
	}
}
