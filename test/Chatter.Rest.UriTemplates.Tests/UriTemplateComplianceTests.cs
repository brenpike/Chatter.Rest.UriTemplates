using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace Chatter.Rest.UriTemplates.Tests
{
    public class UriTemplateComplianceTests
    {
        private const string SpecExamplesFile = "spec-examples.json";
        private const string ExtendedTestsFile = "extended-tests.json";
        private const string NegativeTestsFile = "negative-tests.json";
        private const string SpecExamplesBySectionFile = "spec-examples-by-section.json";

        private static readonly string[] ExercisedTestDataFiles = new[]
        {
            SpecExamplesFile,
            ExtendedTestsFile,
            NegativeTestsFile,
            SpecExamplesBySectionFile
        };

        // --------------------------------------------------------------------
        // 1. Spec Examples — all rows must pass
        // --------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(SpecExamplesData))]
        public void SpecExamples_ExpandCorrectly(string section, string template, object expected)
        {
            AssertPositiveCase(SpecExamplesFile, section, template, expected);
        }

        public static IEnumerable<object[]> SpecExamplesData() =>
            LoadPositiveTestCases(SpecExamplesFile);

        // --------------------------------------------------------------------
        // 2. Extended Tests — some rows may fail (known gaps)
        // --------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(ExtendedTestsData))]
        public void ExtendedTests_ExpandCorrectly(string section, string template, object expected)
        {
            AssertPositiveCase(ExtendedTestsFile, section, template, expected);
        }

        public static IEnumerable<object[]> ExtendedTestsData() =>
            LoadPositiveTestCases(ExtendedTestsFile);

        // --------------------------------------------------------------------
        // 3. Negative Tests — templates must be rejected
        // --------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(NegativeTestsData))]
        public void NegativeTests_RejectInvalidTemplates(string section, string template)
        {
            var variables = LoadVariablesForSection(NegativeTestsFile, section);

            Action act = () =>
            {
                var t = new UriTemplate(template);
                t.Expand(variables);
            };

            act.Should().Throw<Exception>(
                because: "template '{0}' in section '{1}' should be rejected", template, section)
                .Which.Should().Match<Exception>(
                    e => e is FormatException || e is NotSupportedException,
                    because: "only FormatException or NotSupportedException are expected for invalid templates");
        }

        public static IEnumerable<object[]> NegativeTestsData()
        {
            using var doc = LoadTestFile(NegativeTestsFile);

            foreach (var sectionProp in doc.RootElement.EnumerateObject())
            {
                var sectionName = sectionProp.Name;
                var sectionObj = sectionProp.Value;

                if (!sectionObj.TryGetProperty("testcases", out var testcases))
                    continue;

                foreach (var testcase in testcases.EnumerateArray())
                {
                    var templateEl = testcase[0];
                    var expectedEl = testcase[1];

                    // Only include rows where expected is false (negative tests)
                    if (expectedEl.ValueKind == JsonValueKind.False)
                    {
                        yield return new object[] { sectionName, templateEl.GetString()! };
                    }
                }
            }
        }

        // --------------------------------------------------------------------
        // 4. Spec Examples By Section — all rows must pass
        // --------------------------------------------------------------------

        [Theory]
        [MemberData(nameof(SpecExamplesBySectionData))]
        public void SpecExamplesBySection_ExpandCorrectly(string section, string template, object expected)
        {
            AssertPositiveCase(SpecExamplesBySectionFile, section, template, expected);
        }

        public static IEnumerable<object[]> SpecExamplesBySectionData() =>
            LoadPositiveTestCases(SpecExamplesBySectionFile);

        [Fact]
        public void LoadTestFile_MissingFile_NamesSubmoduleRemedy()
        {
            Action act = () => LoadTestFile("missing-compliance-test-data.json");

            act.Should().Throw<FileNotFoundException>()
                .WithMessage("*missing-compliance-test-data.json*git submodule update --init*");
        }

        [Fact]
        public void AllCopiedTestDataFiles_AreExercisedByATheory()
        {
            var copiedFiles = Directory
                .GetFiles(Path.Combine(AppContext.BaseDirectory, "TestData"), "*.json")
                .Select(path => Path.GetFileName(path))
                .ToArray();

            copiedFiles.Should().BeEquivalentTo(
                ExercisedTestDataFiles,
                because: "every *.json fixture the test project copies from the uritemplate-test submodule must be loaded by a theory");
        }

        // ====================================================================
        // Helpers
        // ====================================================================

        private static void AssertPositiveCase(
            string filename, string section, string template, object expected)
        {
            var variables = LoadVariablesForSection(filename, section);
            var result = new UriTemplate(template).Expand(variables);

            switch (expected)
            {
                case string s:
                    result.Should().Be(s,
                        because: "template '{0}' in section '{1}' of '{2}'", template, section, filename);
                    break;

                case string[] arr:
                    result.Should().BeOneOf(arr,
                        because: "template '{0}' in section '{1}' of '{2}'", template, section, filename);
                    break;

                default:
                {
                    var actualType = expected is null ? "null" : expected.GetType().FullName;
                    throw new InvalidOperationException(
                        $"Unsupported expected value type '{actualType}' for template '{template}' in section '{section}' of '{filename}'. A positive compliance row must supply a string or a string[].");
                }
            }
        }

        private static JsonDocument LoadTestFile(string filename)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "TestData", filename);

            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    $"URI template compliance test data was not found at '{path}'. Run 'git submodule update --init' from the repository root and rebuild the test project.",
                    path);
            }

            return JsonDocument.Parse(File.ReadAllText(path));
        }

        private static Dictionary<string, UriTemplateValue> LoadVariablesForSection(
            string filename, string sectionName)
        {
            using var doc = LoadTestFile(filename);
            var section = doc.RootElement.GetProperty(sectionName);
            var variablesEl = section.GetProperty("variables");
            return ParseVariables(variablesEl);
        }

        private static IEnumerable<object[]> LoadPositiveTestCases(string filename)
        {
            using var doc = LoadTestFile(filename);

            foreach (var sectionProp in doc.RootElement.EnumerateObject())
            {
                var sectionName = sectionProp.Name;
                var sectionObj = sectionProp.Value;

                if (!sectionObj.TryGetProperty("testcases", out var testcases))
                    continue;

                foreach (var testcase in testcases.EnumerateArray())
                {
                    var templateEl = testcase[0];
                    var expectedEl = testcase[1];

                    // Skip negative test rows (expected == false)
                    if (expectedEl.ValueKind == JsonValueKind.False)
                        continue;

                    var template = templateEl.GetString()!;
                    object expected;

                    if (expectedEl.ValueKind == JsonValueKind.String)
                    {
                        expected = expectedEl.GetString()!;
                    }
                    else if (expectedEl.ValueKind == JsonValueKind.Array)
                    {
                        expected = expectedEl.EnumerateArray()
                            .Select(e => e.GetString()!)
                            .ToArray();
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"Unexpected expected value kind '{expectedEl.ValueKind}' for template '{template}' in section '{sectionProp.Name}'.");
                    }

                    yield return new object[] { sectionName, template, expected };
                }
            }
        }

        private static Dictionary<string, UriTemplateValue> ParseVariables(JsonElement variablesElement)
        {
            var dict = new Dictionary<string, UriTemplateValue>(StringComparer.Ordinal);

            foreach (var prop in variablesElement.EnumerateObject())
            {
                var name = prop.Name;
                var el = prop.Value;

                switch (el.ValueKind)
                {
                    case JsonValueKind.String:
                        dict[name] = UriTemplateValue.From(el.GetString()!);
                        break;

                    case JsonValueKind.Number:
                    {
                        double d = el.GetDouble();
                        string s = d % 1 == 0
                            ? ((long)d).ToString(CultureInfo.InvariantCulture)
                            : d.ToString(CultureInfo.InvariantCulture);
                        dict[name] = UriTemplateValue.From(s);
                        break;
                    }

                    case JsonValueKind.Array:
                    {
                        var list = new List<string>();
                        foreach (var item in el.EnumerateArray())
                        {
                            list.Add(item.GetString()!);
                        }
                        dict[name] = UriTemplateValue.From(list);
                        break;
                    }

                    case JsonValueKind.Object:
                    {
                        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var kvp in el.EnumerateObject())
                        {
                            pairs[kvp.Name] = kvp.Value.GetString()!;
                        }
                        dict[name] = UriTemplateValue.From(pairs);
                        break;
                    }

                    // Null or Undefined — treat as undefined (omit)
                    case JsonValueKind.Null:
                    case JsonValueKind.Undefined:
                        break;
                }
            }

            return dict;
        }
    }
}
