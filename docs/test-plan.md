# Chatter.Rest.UriTemplates RFC 6570 Test Plan

Spec: https://datatracker.ietf.org/doc/html/rfc6570  
Canonical test suite: https://github.com/uri-templates/uritemplate-test

Coverage status:

- Existing: test is implemented in the current test suite.
- Planned: missing test for behavior the current Level 1-3 API should support.
- Gap: test documents an implementation or documentation gap that must be fixed before the test can pass.
- Deferred: RFC 6570 Level 4 behavior intentionally out of scope for the current `IDictionary<string, string>` API.

## RFC Review Findings

The current implementation covers the primary RFC 6570 Level 1-3 expansion operators for simple string values. The following gaps remain and should drive future work:

| Area | RFC basis | Current state | Required follow-up |
|---|---|---|---|
| Literal expansion | Sections 2.1 and 3.1 require literals outside expressions to be valid URI-template literals and to percent-encode non-URI characters as UTF-8. | Literal text is copied unchanged. Spaces, non-ASCII literals, invalid `%` triplets, and lone `}` are not validated or encoded. | Add literal validation/encoding policy and tests. |
| Variable-name grammar | Section 2.3 defines `varname = varchar *( ["."] varchar )`, where `varchar = ALPHA / DIGIT / "_" / pct-encoded`. | Parser accepts almost any string, trims whitespace, allows empty varspecs, allows invalid dot placement, and allows invalid `%` sequences. | Validate variable-list grammar and distinguish malformed varspecs from valid-but-unsupported Level 4 modifiers. |
| Reserved operators | Section 2.2 reserves `=`, `,`, `!`, `@`, and `|` as operators for future extensions. | These are parsed as part of a variable name instead of rejected or reported as unsupported. | Detect reserved operators explicitly and throw `NotSupportedException` or `FormatException` consistently. |
| Reserved expansion `%` handling | Section 3.2.1 allows `%` through only as part of pct-encoded triplets for `+` and `#`; bare `%` must become `%25`. | `EncodeReserved` preserves bare `%` because `%` is treated as a reserved character. | Fix reserved encoding and add `half = "50%"`, invalid percent, and existing triplet tests. |
| Undefined null values | Section 2.3 allows unknown or null values to be treated as undefined; Section 3.2.1 says undefined variables are ignored. | A dictionary entry whose value is `null` causes `NullReferenceException`. | Treat null dictionary values as undefined or explicitly reject them; tests should lock the chosen RFC-compatible behavior. |
| Case-sensitive lookup | Section 2.3 says variable names are case-sensitive. | Lookup depends on the caller's `IDictionary` comparer; a case-insensitive dictionary can expand `{Var}` from `var`. | Copy input to an ordinal dictionary or otherwise enforce ordinal lookup. |
| Query/path parameter names | Sections 3.2.7-3.2.9 append the variable name encoded as a literal string. | Current behavior is correct for simple valid ASCII names, but invalid names are not rejected and pct-encoded names are not covered by tests. | Add tests for dotted names, pct-encoded names, and invalid names. |
| Canonical RFC examples | RFC Sections 3.2.2-3.2.9 include examples using `who`, `half`, `base`, `dub`, `v`, `list`, `keys`, and `empty_keys`. | Only a subset using `var`, `hello`, `empty`, `path`, `x`, and `y` is tested. | Add all Level 1-3 simple-string canonical examples; Level 4/list/dictionary examples remain deferred. |
| Official test suite | The URI Templates community test suite covers broader syntax and edge cases. | The plan links it, but the project does not consume it. | Add a data-driven compliance test harness for Level 1-3 cases and expected failures/deferred cases. |
| Docs accuracy | `docs/architecture.md` and `docs/usage.md` describe literal text as unchanged and list `%` as reserved passthrough. | These statements conflict with RFC literal expansion and reserved `%` rules. | Update docs when implementation behavior is fixed. |

## Shared Fixtures

Use RFC 6570 Section 3.2 example values where possible:

```csharp
var Variables = new Dictionary<string, string>
{
    ["count"] = "one,two,three", // string-only approximation; true list is Level 4/deferred
    ["dom"] = "example.com",     // string-only approximation; true list is Level 4/deferred
    ["dub"] = "me/too",
    ["hello"] = "Hello World!",
    ["half"] = "50%",
    ["var"] = "value",
    ["who"] = "fred",
    ["base"] = "http://example.com/home/",
    ["path"] = "/foo/bar",
    ["v"] = "6",
    ["x"] = "1024",
    ["y"] = "768",
    ["empty"] = "",
};
// "undef" is intentionally absent.
```

Level 4 list/dictionary fixtures from the RFC:

```csharp
list := ("red", "green", "blue")
keys := [("semi",";"),("dot","."),("comma",",")]
empty_keys := []
```

These require a future value model and are listed in the Level 4 deferred section.

## 1. Level 1 Simple String Expansion `{var}`

Class: `UriTemplateLevel1Tests`

Operator: none. Encoding: unreserved. Separator: `,`. Prefix: none.

### 1.1 RFC canonical examples

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `SingleVar_SimpleValue` | `{var}` | `value` |
| Existing | `SingleVar_WithSpaceAndBang` | `{hello}` | `Hello%20World%21` |
| Existing | `SingleVar_PercentEncoded` | `{half}` | `50%25` |
| Existing | `SingleVar_EmptyValue` | `{empty}` | *(empty string)* |
| Existing | `SingleVar_Undefined` | `{undef}` | *(empty string)* |
| Existing | `SingleVar_EmptyValueWrappedByLiterals` | `O{empty}X` | `OX` |
| Existing | `SingleVar_UndefinedWrappedByLiterals` | `O{undef}X` | `OX` |
| Existing | `SingleVar_WithSlashes` | `{path}` | `%2Ffoo%2Fbar` |
| Existing | `NoOp_TwoVars` | `{x,y}` | `1024,768` |
| Existing | `NoOp_ThreeVars` | `{x,hello,y}` | `1024,Hello%20World%21,768` |
| Existing | `MultipleVars_WithEmpty` | `?{x,empty}` | `?1024,` |
| Existing | `MultipleVars_WithUndefinedTail` | `?{x,undef}` | `?1024` |
| Existing | `MultipleVars_WithUndefinedHead` | `?{undef,y}` | `?768` |

Note: the multi-variable simple expansion rows are currently covered by `UriTemplateLevel3Tests`, where this suite groups Level 3 multi-variable behavior.

### 1.2 Literal text preservation and literal encoding

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `NoExpression_LiteralOnly` | `/orders/list` | `/orders/list` |
| Existing | `LeadingLiteral` | `/orders/{var}` | `/orders/value` |
| Existing | `TrailingLiteral` | `{var}/orders` | `value/orders` |
| Existing | `MiddleLiteral` | `/orders/{var}/items` | `/orders/value/items` |
| Existing | `EmptyTemplate` | *(empty string)* | *(empty string)* |
| Existing | `Literal_PctTripletPreserved` | `/already/%7Eencoded` | `/already/%7Eencoded` |
| Gap | `Literal_NonAsciiEncoded` | `/caf\u00e9/{var}` | `/caf%C3%A9/value` |
| Gap | `Literal_SpaceRejected` | `/bad literal/{var}` | `FormatException` |
| Gap | `Literal_InvalidPercentTripletRejected` | `/bad/%zz/{var}` | `FormatException` |
| Gap | `Literal_LoneClosingBraceRejected` | `/orders/}x` | `FormatException` |

### 1.3 Multiple expressions

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `TwoExpressions` | `/orders/{x}/items/{y}` | `/orders/1024/items/768` |
| Existing | `ConsecutiveExpressions` | `{x}{y}` | `1024768` |
| Existing | `RepeatedVariable_StaticValue` | `{var}/{var}` | `value/value` |
| Existing | `SameVariableDifferentOperators` | `{path}{+path}{/path}` | `%2Ffoo%2Fbar/foo/bar/%2Ffoo%2Fbar` |

### 1.4 Encoding edge cases

| Status | Test name | Variables | Expected |
|---|---|---|---|
| Existing | `Encoding_SpaceEncoded` | `hello="Hello World!"` | `Hello%20World%21` |
| Existing | `Encoding_SlashEncoded` | `path="/foo/bar"` | `%2Ffoo%2Fbar` |
| Existing | `Encoding_TildeNotEncoded` | `var="val~ue"` | `val~ue` |
| Existing | `Encoding_HyphenNotEncoded` | `var="val-ue"` | `val-ue` |
| Existing | `Encoding_DotNotEncoded` | `var="val.ue"` | `val.ue` |
| Existing | `Encoding_UnderscoreNotEncoded` | `var="val_ue"` | `val_ue` |
| Existing | `Encoding_ReservedColonEncoded` | `var="val:ue"` | `val%3Aue` |
| Existing | `Encoding_ReservedAmpersandEncoded` | `var="a&b"` | `a%26b` |
| Existing | `Encoding_PercentEncoded` | `half="50%"` | `50%25` |
| Existing | `Encoding_UnicodeUtf8Encoded` | `var="caf\u00e9"` | `caf%C3%A9` |
| Existing | `Encoding_EmojiUtf8Encoded` | `var="\uD83D\uDE00"` | `%F0%9F%98%80` |
| Existing | `Encoding_ExistingPctTripletEncodedForSimple` | `var="%7E"` | `%257E` |

### 1.5 Guard conditions

| Status | Test name | Input | Expected |
|---|---|---|---|
| Existing | `NullDictionary_Throws` | `variables = null` | `ArgumentNullException` |
| Existing | `EmptyDictionary_AllExpressionsEmpty` | `{var}`, `{}` | *(empty string)* |
| Gap | `NullDictionaryValue_TreatedAsUndefined` | `["var"] = null` | *(empty string)* |
| Existing | `TupleOverload_NullArrayThrows` | `variables = null` | `ArgumentNullException` |
| Existing | `TupleOverload_DuplicateKeys_FirstWins` | `("var","first"),("var","second")` | `first` |

## 2. Level 2 Reserved and Fragment Expansion

Class: `UriTemplateLevel2Tests`

### 2.1 Reserved expansion `{+var}`

Operator: `+`. Encoding: reserved. Separator: `,`. Prefix: none.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Plus_SimpleValue` | `{+var}` | `value` |
| Existing | `Plus_WithSpace` | `{+hello}` | `Hello%20World!` |
| Gap | `Plus_PercentEncoded` | `{+half}` | `50%25` |
| Existing | `Plus_BaseReservedCharsPreserved` | `{+base}index` | `http://example.com/home/index` |
| Existing | `Simple_BaseReservedCharsEncoded` | `{base}index` | `http%3A%2F%2Fexample.com%2Fhome%2Findex` |
| Existing | `Plus_WithSlashes_PreservesSlashes` | `{+path}` | `/foo/bar` |
| Existing | `Plus_TrailingLiteral` | `{+path}/here` | `/foo/bar/here` |
| Existing | `Plus_InQueryContext` | `here?ref={+path}` | `here?ref=/foo/bar` |
| Existing | `Plus_AdjacentSimpleExpression` | `up{+path}{var}/here` | `up/foo/barvalue/here` |
| Existing | `Plus_EmptyValue` | `{+empty}` | *(empty string)* |
| Existing | `Plus_Undefined` | `{+undef}` | *(empty string)* |
| Existing | `Plus_EmptyValueWrappedByLiterals` | `O{+empty}X` | `OX` |
| Existing | `Plus_UndefinedWrappedByLiterals` | `O{+undef}X` | `OX` |
| Existing | `Plus_MultipleVars` | `{+x,hello,y}` | `1024,Hello%20World!,768` |
| Existing | `Plus_MultipleVarsWithPath` | `{+path,x}/here` | `/foo/bar,1024/here` |
| Existing | `Plus_AmpersandPreserved` | `{+var}`, `var="a&b"` | `a&b` |
| Existing | `Plus_ColonPreserved` | `{+var}`, `var="a:b"` | `a:b` |
| Existing | `Plus_ExistingPctTripletPreserved` | `{+var}`, `var="x%2Fy"` | `x%2Fy` |
| Gap | `Plus_BarePercentEncoded` | `{+var}`, `var="x%y"` | `x%25y` |
| Gap | `Plus_InvalidPctTripletEncoded` | `{+var}`, `var="x%zz"` | `x%25zz` |

### 2.2 Fragment expansion `{#var}`

Operator: `#`. Encoding: reserved. Separator: `,`. Prefix: `#` when at least one variable is defined.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Hash_SimpleValue` | `{#var}` | `#value` |
| Existing | `Hash_WithSpace` | `{#hello}` | `#Hello%20World!` |
| Gap | `Hash_PercentEncoded` | `{#half}` | `#50%25` |
| Existing | `Hash_WithSlashes` | `{#path}` | `#/foo/bar` |
| Existing | `Hash_TrailingLiteral` | `{#path,x}/here` | `#/foo/bar,1024/here` |
| Existing | `Hash_MultipleVars` | `{#x,hello,y}` | `#1024,Hello%20World!,768` |
| Existing | `Hash_EmptyValue` | `{#empty}` | `#` |
| Existing | `Hash_Undefined_NoHash` | `{#undef}` | *(empty string)* |
| Existing | `Hash_EmptyValueWrappedByLiteral` | `foo{#empty}` | `foo#` |
| Existing | `Hash_UndefinedWrappedByLiteral` | `foo{#undef}` | `foo` |
| Existing | `Hash_ExistingPctTripletPreserved` | `{#var}`, `var="x%2Fy"` | `#x%2Fy` |
| Gap | `Hash_BarePercentEncoded` | `{#var}`, `var="x%y"` | `#x%25y` |

## 3. Level 3 Multiple Variables and Operator Expansion

Class: `UriTemplateLevel3Tests`

### 3.1 No-operator multi-variable `{x,y}`

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `NoOp_TwoVars` | `{x,y}` | `1024,768` |
| Existing | `NoOp_ThreeVars` | `{x,hello,y}` | `1024,Hello%20World%21,768` |
| Existing | `NoOp_WithUndefined_OmitsUndefined` | `{x,undef,y}` | `1024,768` |
| Existing | `NoOp_AllUndefined` | `{undef,undef}` | *(empty string)* |
| Existing | `NoOp_WithEmpty` | `{x,empty,y}` | `1024,,768` |
| Existing | `NoOp_PercentValue` | `{half,who}` | `50%25,fred` |
| Existing | `NoOp_SlashValueEncoded` | `{who,dub}` | `fred,me%2Ftoo` |

### 3.2 Reserved multi-variable `{+x,y}`

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Plus_TwoVars` | `{+x,hello,y}` | `1024,Hello%20World!,768` |
| Existing | `Plus_WithPath` | `{+path,x}/here` | `/foo/bar,1024/here` |
| Gap | `Plus_PercentMultiVar` | `{+half,who}` | `50%25,fred` |

### 3.3 Fragment multi-variable `{#x,y}`

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Hash_TwoVars` | `{#x,hello,y}` | `#1024,Hello%20World!,768` |
| Gap | `Hash_PercentMultiVar` | `{#half,who}` | `#50%25,fred` |

### 3.4 Label expansion `{.var}`

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Dot_SingleVar` | `{.var}` | `.value` |
| Existing | `Dot_TwoVars` | `{.x,y}` | `.1024.768` |
| Existing | `Dot_Undefined` | `{.undef}` | *(empty string)* |
| Existing | `Dot_EmptyValue` | `{.empty}` | `.` |
| Existing | `Dot_SingleVarWrappedByLiteral` | `X{.var}` | `X.value` |
| Existing | `Dot_TwoVarsWrappedByLiteral` | `X{.x,y}` | `X.1024.768` |
| Existing | `Dot_UndefinedWrappedByLiteral` | `X{.undef}` | `X` |
| Existing | `Dot_EmptyValueWrappedByLiteral` | `X{.empty}` | `X.` |
| Existing | `Dot_InPath` | `/api{.format}` | `/api.value` |
| Existing | `Dot_MixedDefinedUndefined` | `{.x,undef,y}` | `.1024.768` |
| Existing | `Dot_PercentValue` | `{.half,who}` | `.50%25.fred` |
| Existing | `Dot_ValueContainingDotAddsLabels` | `{.var}`, `var="a.b"` | `.a.b` |

### 3.5 Path segment expansion `{/var}`

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Slash_SingleVar` | `{/var}` | `/value` |
| Existing | `Slash_TwoVars` | `{/var,x}` | `/value/1024` |
| Existing | `Slash_Undefined` | `{/undef}` | *(empty string)* |
| Existing | `Slash_EmptyValue` | `{/empty}` | `/` |
| Existing | `Slash_InPath` | `/base{/var}` | `/base/value` |
| Existing | `Slash_MixedDefinedUndefined` | `{/x,undef,y}` | `/1024/768` |
| Existing | `Slash_PercentValue` | `{/half,who}` | `/50%25/fred` |
| Existing | `Slash_ValueContainingSlashEncoded` | `{/who,dub}` | `/fred/me%2Ftoo` |
| Existing | `Slash_VarEmptyAndUndefined` | `{/var,empty,undef}` | `/value/` |

### 3.6 Path-style parameter expansion `{;var}`

Empty value rule: variable name is included without `=`.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Semicolon_SingleVar` | `{;x}` | `;x=1024` |
| Existing | `Semicolon_MultipleVars` | `{;x,y,empty}` | `;x=1024;y=768;empty` |
| Existing | `Semicolon_EmptyValue_NoEquals` | `{;empty}` | `;empty` |
| Existing | `Semicolon_Undefined_Omitted` | `{;undef}` | *(empty string)* |
| Existing | `Semicolon_MixedDefinedUndefined` | `{;x,undef,y}` | `;x=1024;y=768` |
| Existing | `Semicolon_RfcNames` | `{;v,empty,who}` | `;v=6;empty;who=fred` |
| Existing | `Semicolon_UndefinedMiddle` | `{;v,bar,who}` | `;v=6;who=fred` |
| Existing | `Semicolon_PercentValue` | `{;half}` | `;half=50%25` |
| Existing | `Semicolon_DottedVarName` | `{;a.b}`, `["a.b"]="1"` | `;a.b=1` |
| Existing | `Semicolon_PctEncodedVarName` | `{;%78}`, `["%78"]="1"` | `;%78=1` |

### 3.7 Form-style query expansion `{?var}`

Empty value rule: variable name is included with `=` and no value.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Query_SingleVar` | `{?x}` | `?x=1024` |
| Existing | `Query_TwoVars` | `{?x,y}` | `?x=1024&y=768` |
| Existing | `Query_WithEmpty` | `{?x,y,empty}` | `?x=1024&y=768&empty=` |
| Existing | `Query_WithUndefined_OmitsUndefined` | `{?x,y,undef}` | `?x=1024&y=768` |
| Existing | `Query_Undefined_NoPrefix` | `{?undef}` | *(empty string)* |
| Existing | `Query_EmptyOnly` | `{?empty}` | `?empty=` |
| Existing | `Query_MixedDefinedUndefined` | `{?x,undef,y}` | `?x=1024&y=768` |
| Existing | `Query_InFullPath` | `/orders{?x,y}` | `/orders?x=1024&y=768` |
| Existing | `Query_RfcWho` | `{?who}` | `?who=fred` |
| Existing | `Query_PercentValue` | `{?half}` | `?half=50%25` |
| Existing | `Query_DottedVarName` | `{?a.b}`, `["a.b"]="1"` | `?a.b=1` |
| Existing | `Query_PctEncodedVarName` | `{?%78}`, `["%78"]="1"` | `?%78=1` |

### 3.8 Form-style query continuation expansion `{&var}`

Empty value rule: same as `?`.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Ampersand_SingleVar` | `{&x}` | `&x=1024` |
| Existing | `Ampersand_TwoVars` | `{&x,y}` | `&x=1024&y=768` |
| Existing | `Ampersand_WithEmpty` | `{&x,y,empty}` | `&x=1024&y=768&empty=` |
| Existing | `Ampersand_WithUndefined_OmitsUndefined` | `{&x,y,undef}` | `&x=1024&y=768` |
| Existing | `Ampersand_Undefined_NoPrefix` | `{&undef}` | *(empty string)* |
| Existing | `Ampersand_InQueryString` | `/orders?sort=date{&x,y}` | `/orders?sort=date&x=1024&y=768` |
| Existing | `Ampersand_RfcFixedQuery` | `?fixed=yes{&x}` | `?fixed=yes&x=1024` |
| Existing | `Ampersand_PercentValue` | `{&half}` | `&half=50%25` |
| Existing | `Ampersand_DottedVarName` | `{&a.b}`, `["a.b"]="1"` | `&a.b=1` |
| Existing | `Ampersand_PctEncodedVarName` | `{&%78}`, `["%78"]="1"` | `&%78=1` |

## 4. Variable Discovery

Class: `UriTemplateGetVariablesTests`

`UriTemplate.GetVariables()` should return all variable names in template order, deduplicated using ordinal case-sensitive comparison.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `SingleLevel1Var` | `{var}` | `["var"]` |
| Existing | `MultipleLevel1Vars` | `{x,y}` | `["x", "y"]` |
| Existing | `Level2PlusVar` | `{+path}` | `["path"]` |
| Existing | `Level2HashVar` | `{#var}` | `["var"]` |
| Existing | `Level3QueryVars` | `{?status,page}` | `["status", "page"]` |
| Existing | `MixedExpressions` | `/orders/{id}{?status,page}` | `["id", "status", "page"]` |
| Existing | `DeduplicatesAcrossExpressions` | `{x}/foo/{x}` | `["x"]` |
| Existing | `NoExpressions` | `/literal` | `[]` |
| Existing | `AllOperators` | `{a}{+b}{#c}{.d}{/e}{;f}{?g}{&h}` | `["a","b","c","d","e","f","g","h"]` |
| Existing | `CaseSensitiveDistinctNames` | `{var,Var}` | `["var", "Var"]` |
| Existing | `DottedAndPctEncodedNames` | `{a.b,%78}` | `["a.b", "%78"]` |
| Deferred | `Level4ModifiersReturnBaseName` | `{var:3}{list*}` | `["var", "list"]` after Level 4 support exists |

## 5. Parser and Edge Cases

Class: `UriTemplateEdgeCaseTests`

### 5.1 Template parsing shape

| Status | Test name | Input | Expected |
|---|---|---|---|
| Existing | `EmptyTemplate` | `` | `` |
| Existing | `LiteralOnly` | `/orders/list` | `/orders/list` |
| Existing | `ExpressionAtStart` | `{var}/rest` | `value/rest` |
| Existing | `ExpressionAtEnd` | `/prefix/{var}` | `/prefix/value` |
| Existing | `ExpressionOnly` | `{var}` | `value` |
| Existing | `ConsecutiveExpressions` | `{x}{y}` | `1024768` |
| Existing | `MultipleExpressionsWithLiterals` | `/a/{x}/b/{y}/c` | `/a/1024/b/768/c` |

### 5.2 Mixed-level expressions in one template

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Level1AndLevel3Query` | `/orders/{id}{?status,page}` | `/orders/42?status=open&page=2` |
| Existing | `Level1AndLevel2Reserved` | `/proxy/{+path}/tail` | `/proxy/foo/bar/tail` |
| Existing | `Level2AndLevel3` | `{+base}{/segment}` | `/root/value` |
| Existing | `MixedReservedAndSimpleEncoding` | `{+base}{var}{?half}` | `http://example.com/home/value?half=50%25` |

### 5.3 Malformed expression handling

The library currently chooses exceptions for malformed templates. Preserve that policy unless a future API intentionally exposes RFC diagnostic partial expansion.

| Status | Test name | Input | Expected |
|---|---|---|---|
| Existing | `UnclosedBrace_Throws` | `/orders/{id` | `FormatException` |
| Existing | `NestedBraces_Throws` | `/orders/{{id}}` | `FormatException` |
| Existing | `EmptyExpression_Throws` | `/orders/{}` | `FormatException` |
| Gap | `OperatorOnlyQuery_Throws` | `{?}` | `FormatException` |
| Gap | `TrailingComma_Throws` | `{x,}` | `FormatException` |
| Gap | `LeadingComma_Throws` | `{,x}` | `NotSupportedException` |
| Gap | `DoubleComma_Throws` | `{x,,y}` | `FormatException` |
| Gap | `WhitespaceInExpression_Throws` | `{ x }` | `FormatException` |
| Gap | `WhitespaceAfterComma_Throws` | `{x, y}` | `FormatException` |
| Gap | `InvalidVarNameHyphen_Throws` | `{bad-name}` | `FormatException` |
| Gap | `InvalidVarNameDollar_Throws` | `{bad$name}` | `FormatException` |
| Gap | `InvalidVarNameSlash_Throws` | `{bad/name}` | `FormatException` |
| Gap | `InvalidVarNameConsecutiveDots_Throws` | `{a..b}` | `FormatException` |
| Gap | `InvalidVarNameTrailingDot_Throws` | `{a.}` | `FormatException` |
| Gap | `InvalidVarNameLeadingDotNoOperator_Throws` | `{.}` | `FormatException` |
| Gap | `InvalidPctEncodedVarName_Throws` | `{%zz}` | `FormatException` |
| Gap | `DoubleOperator_Throws` | `{??x}` | `FormatException` |

### 5.4 Reserved future operators

RFC 6570 reserves `=`, `,`, `!`, `@`, and `|` as operator characters. The current API should reject them clearly until any extension is intentionally supported.

| Status | Test name | Input | Expected |
|---|---|---|---|
| Gap | `ReservedEqualsOperator_Throws` | `{=var}` | `NotSupportedException` |
| Gap | `ReservedCommaOperator_Throws` | `{,var}` | `NotSupportedException` |
| Gap | `ReservedBangOperator_Throws` | `{!var}` | `NotSupportedException` |
| Gap | `ReservedAtOperator_Throws` | `{@var}` | `NotSupportedException` |
| Gap | `ReservedPipeOperator_Throws` | `{|var}` | `NotSupportedException` |

### 5.5 Level 4 detection and malformed modifiers

Valid Level 4 modifiers are unsupported by this package today. Malformed modifier syntax should be a format error, not silently treated as a variable name.

| Status | Test name | Input | Expected |
|---|---|---|---|
| Existing | `PrefixModifier_Throws` | `{var:3}` | `NotSupportedException` |
| Existing | `ExplodeModifier_Throws` | `{list*}` | `NotSupportedException` |
| Existing | `ExplodeWithOperator_Throws` | `{/list*}` | `NotSupportedException` |
| Existing | `PrefixModifierMaxLengthFourDigits_Throws` | `{var:9999}` | `NotSupportedException` |
| Gap | `PrefixModifierZero_ThrowsFormat` | `{var:0}` | `FormatException` |
| Gap | `PrefixModifierTooLarge_ThrowsFormat` | `{var:10000}` | `FormatException` |
| Gap | `PrefixModifierNonNumeric_ThrowsFormat` | `{var:abc}` | `FormatException` |
| Gap | `ModifierInMiddle_ThrowsFormat` | `{va*r}` | `FormatException` |
| Gap | `ColonWithoutLength_ThrowsFormat` | `{var:}` | `FormatException` |

### 5.6 Case sensitivity and lookup semantics

| Status | Test name | Description | Expected |
|---|---|---|---|
| Existing | `VariableNames_CaseSensitive` | `{Var}` with `Var="upper"` and `var="lower"` in default dict | `upper` |
| Gap | `CaseInsensitiveDictionaryDoesNotChangeTemplateSemantics` | `{Var}` with case-insensitive dict containing only `var="lower"` | *(empty string)* |
| Existing | `OrdinalDuplicateNamesRemainDistinct` | `{var,Var}` with both values | `lower,upper` |

## 6. Level 4 Deferred Compliance Matrix

Class: future `UriTemplateLevel4Tests`

These tests should remain documented as Deferred until the public API can represent string, list, and associative-array values. The parser should still identify syntactically valid Level 4 templates as unsupported today.

### 6.1 Prefix modifiers

| Status | Test name | Template | Expected after Level 4 |
|---|---|---|---|
| Deferred | `Prefix_Simple` | `{var:3}` | `val` |
| Deferred | `Prefix_LongerThanValue` | `{var:30}` | `value` |
| Deferred | `Prefix_Reserved` | `{+path:6}/here` | `/foo/b/here` |
| Deferred | `Prefix_Fragment` | `{#path:6}/here` | `#/foo/b/here` |
| Deferred | `Prefix_Label` | `X{.var:3}` | `X.val` |
| Deferred | `Prefix_Path` | `{/var:1,var}` | `/v/value` |
| Deferred | `Prefix_PathWithPctEncoding` | `{/list*,path:4}` | `/red/green/blue/%2Ffoo` |
| Deferred | `Prefix_Semicolon` | `{;hello:5}` | `;hello=Hello` |
| Deferred | `Prefix_Query` | `{?var:3}` | `?var=val` |
| Deferred | `Prefix_Ampersand` | `{&var:3}` | `&var=val` |
| Deferred | `Prefix_UnicodeCountsCodePoints` | `{var:1}`, `var="\uD83D\uDE00x"` | `%F0%9F%98%80` |
| Deferred | `Prefix_DoesNotSplitPctTriplet` | `{var:2}`, `var="%7Ex"` | `%7E` or documented decoded-value behavior |

### 6.2 List values

| Status | Test name | Template | Expected after Level 4 |
|---|---|---|---|
| Deferred | `List_Simple` | `{list}` | `red,green,blue` |
| Deferred | `List_SimpleExplode` | `{list*}` | `red,green,blue` |
| Deferred | `List_Reserved` | `{+list}` | `red,green,blue` |
| Deferred | `List_ReservedExplode` | `{+list*}` | `red,green,blue` |
| Deferred | `List_Fragment` | `{#list}` | `#red,green,blue` |
| Deferred | `List_FragmentExplode` | `{#list*}` | `#red,green,blue` |
| Deferred | `List_Label` | `X{.list}` | `X.red,green,blue` |
| Deferred | `List_LabelExplode` | `X{.list*}` | `X.red.green.blue` |
| Deferred | `List_Path` | `{/list}` | `/red,green,blue` |
| Deferred | `List_PathExplode` | `{/list*}` | `/red/green/blue` |
| Deferred | `List_Semicolon` | `{;list}` | `;list=red,green,blue` |
| Deferred | `List_SemicolonExplode` | `{;list*}` | `;list=red;list=green;list=blue` |
| Deferred | `List_Query` | `{?list}` | `?list=red,green,blue` |
| Deferred | `List_QueryExplode` | `{?list*}` | `?list=red&list=green&list=blue` |
| Deferred | `List_Ampersand` | `{&list}` | `&list=red,green,blue` |
| Deferred | `List_AmpersandExplode` | `{&list*}` | `&list=red&list=green&list=blue` |
| Deferred | `EmptyList_IsUndefined` | `{?empty_keys}` | *(empty string)* |

### 6.3 Associative arrays

| Status | Test name | Template | Expected after Level 4 |
|---|---|---|---|
| Deferred | `Keys_Simple` | `{keys}` | `semi,%3B,dot,.,comma,%2C` |
| Deferred | `Keys_SimpleExplode` | `{keys*}` | `semi=%3B,dot=.,comma=%2C` |
| Deferred | `Keys_Reserved` | `{+keys}` | `semi,;,dot,.,comma,,` |
| Deferred | `Keys_ReservedExplode` | `{+keys*}` | `semi=;,dot=.,comma=,` |
| Deferred | `Keys_Fragment` | `{#keys}` | `#semi,;,dot,.,comma,,` |
| Deferred | `Keys_FragmentExplode` | `{#keys*}` | `#semi=;,dot=.,comma=,` |
| Deferred | `Keys_Label` | `X{.keys}` | `X.semi,%3B,dot,.,comma,%2C` |
| Deferred | `Keys_LabelExplode` | `X{.keys*}` | `X.semi=%3B.dot=..comma=%2C` |
| Deferred | `Keys_Path` | `{/keys}` | `/semi,%3B,dot,.,comma,%2C` |
| Deferred | `Keys_PathExplode` | `{/keys*}` | `/semi=%3B/dot=./comma=%2C` |
| Deferred | `Keys_Semicolon` | `{;keys}` | `;keys=semi,%3B,dot,.,comma,%2C` |
| Deferred | `Keys_SemicolonExplode` | `{;keys*}` | `;semi=%3B;dot=.;comma=%2C` |
| Deferred | `Keys_Query` | `{?keys}` | `?keys=semi,%3B,dot,.,comma,%2C` |
| Deferred | `Keys_QueryExplode` | `{?keys*}` | `?semi=%3B&dot=.&comma=%2C` |
| Deferred | `Keys_Ampersand` | `{&keys}` | `&keys=semi,%3B,dot,.,comma,%2C` |
| Deferred | `Keys_AmpersandExplode` | `{&keys*}` | `&semi=%3B&dot=.&comma=%2C` |
| Deferred | `EmptyKeys_IsUndefined` | `X{.empty_keys}` | `X` |
| Deferred | `EmptyKeysExploded_IsUndefined` | `X{.empty_keys*}` | `X` |

## 7. Official URI Template Test Suite

Class: future `UriTemplateComplianceTests`

Add a data-driven harness around https://github.com/uri-templates/uritemplate-test.

| Status | Test group | Expected handling |
|---|---|---|
| Planned | `spec-examples.json` Level 1-3 string cases | Must pass. |
| Deferred | `spec-examples.json` Level 4 prefix/list/dictionary cases | Mark skipped until Level 4 API exists. |
| Planned | `extended-tests.json` valid Level 1-3 string cases | Must pass after parser/encoding gaps are closed. |
| Gap | Invalid grammar cases | Must assert `FormatException` or the chosen diagnostic API behavior. |
| Deferred | Explode/list/dictionary cases | Mark skipped until Level 4 API exists. |

## 8. LinkObject Integration Tests

Class: `LinkObjectUriTemplateIntegrationTests` in the HAL repository, not this standalone package.

After `LinkObject` delegates to `UriTemplate`, verify that the public HAL API behaves correctly with Level 2 and 3 templates.

| Status | Test name | Template | `Templated` | Call | Expected |
|---|---|---|---|---|---|
| Planned | `Level2_Plus_ExpandsReservedChars` | `/proxy/{+path}` | true | `Expand(("path", "/foo/bar"))` | `/proxy/foo/bar` |
| Planned | `Level2_Hash_ExpandsFragment` | `/page{#section}` | true | `Expand(("section", "intro"))` | `/page#intro` |
| Planned | `Level3_Query_BuildsQueryString` | `/orders{?status,page}` | true | `Expand(("status","open"),("page","2"))` | `/orders?status=open&page=2` |
| Planned | `Level3_Slash_BuildsPathSegment` | `/base{/segment}` | true | `Expand(("segment","value"))` | `/base/value` |
| Planned | `GetTemplateVariables_Level2_ReturnsVars` | `{+path}` | true | `GetTemplateVariables()` | `["path"]` |
| Planned | `GetTemplateVariables_Level3Query_ReturnsVars` | `{?status,page}` | true | `GetTemplateVariables()` | `["status", "page"]` |
| Planned | `NotTemplated_Level2Syntax_Unchanged` | `{+path}` | false | `Expand(("path","/foo"))` | `{+path}` |

## 9. Coverage Summary

| Area | Existing | Planned | Gap | Deferred |
|---|---:|---:|---:|---:|
| Level 1 simple string | 37 | 0 | 5 | 0 |
| Level 2 reserved and fragment | 28 | 0 | 6 | 0 |
| Level 3 operators | 63 | 0 | 2 | 0 |
| Variable discovery | 11 | 0 | 0 | 1 |
| Parser and edge cases | 21 | 0 | 28 | 0 |
| Level 4 matrix | 0 | 0 | 0 | 45 |
| Official compliance harness | 0 | 2 | 1 | 2 |
| HAL `LinkObject` integration | 0 | 7 | 0 | 0 |

The highest-priority implementation fixes are:

1. Correct reserved expansion handling for bare `%`.
2. Add RFC 6570 variable-name and expression grammar validation.
3. Decide and implement literal validation/encoding behavior.
4. Enforce ordinal case-sensitive lookup independent of caller dictionary comparer.
5. Treat null dictionary values as undefined or document and test a stricter API contract.
