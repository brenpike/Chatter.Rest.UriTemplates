# Chatter.Rest.UriTemplates RFC 6570 Test Plan

Spec: https://datatracker.ietf.org/doc/html/rfc6570  
Canonical test suite: https://github.com/uri-templates/uritemplate-test

Coverage status:

- Existing: test is implemented in the current test suite.
- Planned: missing test for behavior the current Level 1-4 API should support.
- Gap: test documents an implementation or documentation gap that must be fixed before the test can pass.

## RFC Review Findings

The current implementation covers RFC 6570 Level 1-4 expansion operators for simple string values, list values, and associative-array values. The following gaps remain and should drive future work:

| Area | RFC basis | Current state | Required follow-up |
|---|---|---|---|
| Literal expansion | Sections 2.1 and 3.1 require literals outside expressions to be valid URI-template literals and to percent-encode non-URI characters as UTF-8. | Literal text is validated and encoded by `UriTemplateParser.ProcessLiteral`. Every ASCII character that is neither in the Section 2.1 `literals` production nor permitted in a URI by RFC 3986 is rejected with `FormatException` (space, C0 controls, DEL, lone `{`/`}`, `"`, `<`, `>`, `\`, `^`, `` ` ``, `\|`), a bare `%` is rejected unless it starts a valid triplet, and unpaired surrogates are rejected. A non-ASCII character is UTF-8 percent-encoded only when its decoded Unicode scalar falls inside the Section 1.5 `ucschar` (%xA0-D7FF / %xF900-FDCF / %xFDF0-FFEF / %xN0000-NFFFD for N = 1-D / %xE1000-EFFFD) or `iprivate` (%xE000-F8FF / %xF0000-FFFFD / %x100000-10FFFD) ranges, which the Section 2.1 `literals` production references; every other scalar is rejected with `FormatException` (the C1 controls U+0080-U+009F, the noncharacters U+FDD0-U+FDEF and the last two code points of every plane, the specials block U+FFF0-U+FFFF, and the plane-14 tag and variation-selector block U+E0000-U+E0FFF). `Literal_EveryNonAsciiScalar_MatchesUcsCharOrIPrivateSet` checks all 1,111,936 scalars against an independent transcription of both productions. The apostrophe `'` (%x27) is accepted: the Section 2.1 ABNF comment excludes it, but that contradicts Section 2.1's own prose (characters "allowed in a URI" are copied through) and RFC 3986 Section 2.2 lists `'` in `sub-delims`. The official uritemplate-test Level 1 example `'{var}'` requires it. Every other ABNF-excluded character was audited against RFC 3986 and is genuinely forbidden in a URI. | None. |
| Variable-name grammar | Section 2.3 defines `varname = varchar *( ["."] varchar )`, where `varchar = ALPHA / DIGIT / "_" / pct-encoded`. | Parser validates variable names, dot placement, pct-encoded sequences, and Level 4 modifier syntax. | Remaining edge cases around uncommon pct-encoded variable names. |
| Reserved operators | Section 2.2 reserves `=`, `,`, `!`, `@`, and `|` as operators for future extensions. | Detected and rejected with `NotSupportedException`. | None. |
| Reserved expansion `%` handling | Section 3.2.1 allows `%` through only as part of pct-encoded triplets for `+` and `#`; bare `%` must become `%25`. | Correct. `IsReservedChar` deliberately excludes `%`, so a bare `%` is encoded as `%25` while a valid pct-encoded triplet passes through unchanged. | None. Behaviour verified: `{+v}` with `50%` gives `50%25`, with `%2F` gives `%2F`, with `a%zz` gives `a%25zz`. |
| Undefined null values | Section 2.3 allows unknown or null values to be treated as undefined; Section 3.2.1 says undefined variables are ignored. | The `IDictionary<string, object?>` overload treats a null value as undefined and omits the variable (`UriTemplateExpander.Expand`); `UriTemplateSecurityTests.NullValue_TreatedAsUndefined` locks that behavior. The `IDictionary<string, string>` overload preserves the same behavior when wrapping values into the canonical overload; `UriTemplateLevel1Tests.NullDictionaryValue_TreatedAsUndefined` locks that. The `IDictionary<string, UriTemplateValue>` overload also treats a null entry as undefined — `UriTemplate.Expand` maps a null entry to the canonical `null` representation before `UriTemplateExpander.MapValue` is reached, so all overloads agree (issue #21, landed via PR #41); `UriTemplateArgumentContractTests.Expand_UriTemplateValueDictionary_NullEntry_TreatedAsUndefined` locks that behavior. | None. |
| Case-sensitive lookup | Section 2.3 says variable names are case-sensitive. | Resolved. Every public overload copies the caller's variables into a `Dictionary<string, object?>` built with `StringComparer.Ordinal` before expansion, so a case-insensitive input dictionary can no longer satisfy `{Var}` from `var`. | None. |
| Query/path parameter names | Sections 3.2.7-3.2.9 append the variable name encoded as a literal string. | Resolved. `UriTemplateParser.ValidateVarName` enforces the Section 2.3 `varname` production — rejecting leading, trailing and consecutive dots, invalid characters, and malformed pct-encoded triplets — and `UriTemplateParserValidationTests` covers dotted and invalid names. | None. |
| Canonical RFC examples | RFC Sections 3.2.2-3.2.9 include examples using `who`, `half`, `base`, `dub`, `v`, `list`, `keys`, and `empty_keys`. | Resolved. The official suite is consumed by `UriTemplateComplianceTests`, which covers the canonical Level 1-4 examples in addition to the hand-written per-level tests. | None. |
| Official test suite | The URI Templates community test suite covers broader syntax and edge cases. | Resolved. `UriTemplateComplianceTests` is a data-driven harness over the official `uri-templates/uritemplate-test` suite, covering `spec-examples.json`, `extended-tests.json` and `negative-tests.json`. | None. Note the suite data arrives via a git submodule that cannot always be cloned; when it is absent these tests fail rather than skip. |
| Docs accuracy | `docs/architecture.md` previously described literal text as returned unchanged, and `docs/usage.md` previously listed `%` in the reserved pass-through set. | Resolved. `docs/architecture.md` now states that literal text is validated and percent-encoded per Section 2.1, including the non-ASCII scalar rule (only `ucschar` and `iprivate` scalars are encoded; everything else, unpaired surrogates included, is rejected). `docs/usage.md` already states that `%` is not in the pass-through set and that a bare `%` becomes `%25`. | None. |

## Shared Fixtures

Use RFC 6570 Section 3.2 example values where possible:

```csharp
var Variables = new Dictionary<string, string>
{
    ["count"] = "one,two,three", // string-only approximation; see Level 4 tests for true list expansion
    ["dom"] = "example.com",     // string-only approximation; see Level 4 tests for true list expansion
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

Level 4 list/dictionary fixtures from the RFC (used in `UriTemplateLevel4Tests`):

```csharp
list := ("red", "green", "blue")
keys := [("semi",";"),("dot","."),("comma",",")]
empty_keys := []
```

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
| Existing | `Literal_NonAsciiEncoded` | `/caf\u00e9/{var}` | `/caf%C3%A9/value` |
| Existing | `Literal_SpaceRejected` | `/bad literal/{var}` | `FormatException` |
| Existing | `Literal_InvalidPercentTripletRejected` | `/bad/%zz/{var}` | `FormatException` |
| Existing | `Literal_LoneClosingBraceRejected` | `/orders/}x` | `FormatException` |

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
| Existing | `NullDictionaryValue_TreatedAsUndefined` | `["var"] = null` | *(empty string)* |
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
| Existing | `Plus_PercentEncoded` | `{+half}` | `50%25` |
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
| Existing | `Plus_BarePercentEncoded` | `{+var}`, `var="x%y"` | `x%25y` |
| Existing | `Plus_InvalidPctTripletEncoded` | `{+var}`, `var="x%zz"` | `x%25zz` |

### 2.2 Fragment expansion `{#var}`

Operator: `#`. Encoding: reserved. Separator: `,`. Prefix: `#` when at least one variable is defined.

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Hash_SimpleValue` | `{#var}` | `#value` |
| Existing | `Hash_WithSpace` | `{#hello}` | `#Hello%20World!` |
| Existing | `Hash_PercentEncoded` | `{#half}` | `#50%25` |
| Existing | `Hash_WithSlashes` | `{#path}` | `#/foo/bar` |
| Existing | `Hash_TrailingLiteral` | `{#path,x}/here` | `#/foo/bar,1024/here` |
| Existing | `Hash_MultipleVars` | `{#x,hello,y}` | `#1024,Hello%20World!,768` |
| Existing | `Hash_EmptyValue` | `{#empty}` | `#` |
| Existing | `Hash_Undefined_NoHash` | `{#undef}` | *(empty string)* |
| Existing | `Hash_EmptyValueWrappedByLiteral` | `foo{#empty}` | `foo#` |
| Existing | `Hash_UndefinedWrappedByLiteral` | `foo{#undef}` | `foo` |
| Existing | `Hash_ExistingPctTripletPreserved` | `{#var}`, `var="x%2Fy"` | `#x%2Fy` |
| Existing | `Hash_BarePercentEncoded` | `{#var}`, `var="x%y"` | `#x%25y` |

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
| Existing | `Plus_PercentMultiVar` | `{+half,who}` | `50%25,fred` |

### 3.3 Fragment multi-variable `{#x,y}`

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Hash_TwoVars` | `{#x,hello,y}` | `#1024,Hello%20World!,768` |
| Existing | `Hash_PercentMultiVar` | `{#half,who}` | `#50%25,fred` |

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
| Existing | `Level4ModifiersReturnBaseName_PrefixAndExplode` | `{var:3}{list*}` | `["var", "list"]` |
| Existing | `Level4ModifiersReturnBaseName_MixedExpression` | `{/var:1,var}` | `["var"]` |

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
| Existing | `OperatorOnlyQuery_Throws` | `{?}` | `FormatException` |
| Existing | `TrailingComma_Throws` | `{x,}` | `FormatException` |
| Existing | `LeadingComma_Throws` | `{,x}` | `NotSupportedException` |
| Existing | `DoubleComma_Throws` | `{x,,y}` | `FormatException` |
| Existing | `WhitespaceInExpression_Throws` | `{ x }` | `FormatException` |
| Existing | `WhitespaceAfterComma_Throws` | `{x, y}` | `FormatException` |
| Existing | `InvalidVarNameHyphen_Throws` | `{bad-name}` | `FormatException` |
| Existing | `InvalidVarNameDollar_Throws` | `{bad$name}` | `FormatException` |
| Existing | `InvalidVarNameSlash_Throws` | `{bad/name}` | `FormatException` |
| Existing | `InvalidVarNameConsecutiveDots_Throws` | `{a..b}` | `FormatException` |
| Existing | `InvalidVarNameTrailingDot_Throws` | `{a.}` | `FormatException` |
| Existing | `InvalidVarNameLeadingDotNoOperator_Throws` | `{.}` | `FormatException` |
| Existing | `InvalidPctEncodedVarName_Throws` | `{%zz}` | `FormatException` |
| Existing | `DoubleOperator_Throws` | `{??x}` | `FormatException` |

### 5.4 Reserved future operators

RFC 6570 reserves `=`, `,`, `!`, `@`, and `|` as operator characters. The current API should reject them clearly until any extension is intentionally supported.

| Status | Test name | Input | Expected |
|---|---|---|---|
| Existing | `ReservedEqualsOperator_Throws` | `{=var}` | `NotSupportedException` |
| Existing | `ReservedCommaOperator_Throws` | `{,var}` | `NotSupportedException` |
| Existing | `ReservedBangOperator_Throws` | `{!var}` | `NotSupportedException` |
| Existing | `ReservedAtOperator_Throws` | `{@var}` | `NotSupportedException` |
| Existing | `ReservedPipeOperator_Throws` | `{|var}` | `NotSupportedException` |

### 5.5 Level 4 modifier validation

Level 4 modifiers (prefix `:N` and explode `*`) are now fully supported. Malformed modifier syntax remains a `FormatException`. Prefix and explode are mutually exclusive per RFC 6570.

| Status | Test name | Input | Expected |
|---|---|---|---|
| Existing | `PrefixAndExplodeBoth_ThrowsFormat` | `{var:3*}`, `{var*:3}` | `FormatException` (mutual exclusion) |
| Existing | `PrefixModifierZero_ThrowsFormat` | `{var:0}` | `FormatException` |
| Existing | `PrefixModifierTooLarge_ThrowsFormat` | `{var:10000}` | `FormatException` |
| Existing | `PrefixModifierNonNumeric_ThrowsFormat` | `{var:abc}` | `FormatException` |
| Existing | `ModifierInMiddle_ThrowsFormat` | `{va*r}` | `FormatException` |
| Existing | `ColonWithoutLength_ThrowsFormat` | `{var:}` | `FormatException` |

### 5.6 Case sensitivity and lookup semantics

| Status | Test name | Description | Expected |
|---|---|---|---|
| Existing | `VariableNames_CaseSensitive` | `{Var}` with `Var="upper"` and `var="lower"` in default dict | `upper` |
| Existing | `CaseInsensitiveDictionaryDoesNotChangeTemplateSemantics` | `{Var}` with case-insensitive dict containing only `var="lower"` | *(empty string)* |
| Existing | `OrdinalDuplicateNamesRemainDistinct` | `{var,Var}` with both values | `lower,upper` |

## 6. Level 4 Compliance Matrix

Class: `UriTemplateLevel4Tests`

### 6.1 Prefix modifiers

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Prefix_Simple` | `{var:3}` | `val` |
| Existing | `Prefix_LongerThanValue` | `{var:30}` | `value` |
| Existing | `Prefix_Reserved` | `{+path:6}/here` | `/foo/b/here` |
| Existing | `Prefix_Fragment` | `{#path:6}/here` | `#/foo/b/here` |
| Existing | `Prefix_Label` | `X{.var:3}` | `X.val` |
| Existing | `Prefix_Path` | `{/var:1,var}` | `/v/value` |
| Existing | `Prefix_PathWithPctEncoding` | `{/list*,path:4}` | `/red/green/blue/%2Ffoo` |
| Existing | `Prefix_Semicolon` | `{;hello:5}` | `;hello=Hello` |
| Existing | `Prefix_Query` | `{?var:3}` | `?var=val` |
| Existing | `Prefix_Ampersand` | `{&var:3}` | `&var=val` |
| Existing | `Prefix_UnicodeCountsCodePoints` | `{var:1}`, `var="\uD83D\uDE00x"` | `%F0%9F%98%80` |
| Existing | `Prefix_DoesNotSplitPctTriplet` | `{var:2}`, `var="%7Ex"` | `%257Ex` |

### 6.2 List values

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `List_Simple` | `{list}` | `red,green,blue` |
| Existing | `List_SimpleExplode` | `{list*}` | `red,green,blue` |
| Existing | `List_Reserved` | `{+list}` | `red,green,blue` |
| Existing | `List_ReservedExplode` | `{+list*}` | `red,green,blue` |
| Existing | `List_Fragment` | `{#list}` | `#red,green,blue` |
| Existing | `List_FragmentExplode` | `{#list*}` | `#red,green,blue` |
| Existing | `List_Label` | `X{.list}` | `X.red,green,blue` |
| Existing | `List_LabelExplode` | `X{.list*}` | `X.red.green.blue` |
| Existing | `List_Path` | `{/list}` | `/red,green,blue` |
| Existing | `List_PathExplode` | `{/list*}` | `/red/green/blue` |
| Existing | `List_Semicolon` | `{;list}` | `;list=red,green,blue` |
| Existing | `List_SemicolonExplode` | `{;list*}` | `;list=red;list=green;list=blue` |
| Existing | `List_Query` | `{?list}` | `?list=red,green,blue` |
| Existing | `List_QueryExplode` | `{?list*}` | `?list=red&list=green&list=blue` |
| Existing | `List_Ampersand` | `{&list}` | `&list=red,green,blue` |
| Existing | `List_AmpersandExplode` | `{&list*}` | `&list=red&list=green&list=blue` |
| Existing | `EmptyList_IsUndefined` | `{?empty_keys}` | *(empty string)* |

### 6.3 Associative arrays

| Status | Test name | Template | Expected |
|---|---|---|---|
| Existing | `Keys_Simple` | `{keys}` | `semi,%3B,dot,.,comma,%2C` |
| Existing | `Keys_SimpleExplode` | `{keys*}` | `semi=%3B,dot=.,comma=%2C` |
| Existing | `Keys_Reserved` | `{+keys}` | `semi,;,dot,.,comma,,` |
| Existing | `Keys_ReservedExplode` | `{+keys*}` | `semi=;,dot=.,comma=,` |
| Existing | `Keys_Fragment` | `{#keys}` | `#semi,;,dot,.,comma,,` |
| Existing | `Keys_FragmentExplode` | `{#keys*}` | `#semi=;,dot=.,comma=,` |
| Existing | `Keys_Label` | `X{.keys}` | `X.semi,%3B,dot,.,comma,%2C` |
| Existing | `Keys_LabelExplode` | `X{.keys*}` | `X.semi=%3B.dot=..comma=%2C` |
| Existing | `Keys_Path` | `{/keys}` | `/semi,%3B,dot,.,comma,%2C` |
| Existing | `Keys_PathExplode` | `{/keys*}` | `/semi=%3B/dot=./comma=%2C` |
| Existing | `Keys_Semicolon` | `{;keys}` | `;keys=semi,%3B,dot,.,comma,%2C` |
| Existing | `Keys_SemicolonExplode` | `{;keys*}` | `;semi=%3B;dot=.;comma=%2C` |
| Existing | `Keys_Query` | `{?keys}` | `?keys=semi,%3B,dot,.,comma,%2C` |
| Existing | `Keys_QueryExplode` | `{?keys*}` | `?semi=%3B&dot=.&comma=%2C` |
| Existing | `Keys_Ampersand` | `{&keys}` | `&keys=semi,%3B,dot,.,comma,%2C` |
| Existing | `Keys_AmpersandExplode` | `{&keys*}` | `&semi=%3B&dot=.&comma=%2C` |
| Existing | `EmptyKeys_IsUndefined` | `X{.empty_keys}` | `X` |
| Existing | `EmptyKeysExploded_IsUndefined` | `X{.empty_keys*}` | `X` |

### 6.4 Level 4 edge cases

Class: `UriTemplateLevel4Tests` (edge case methods) and `UriTemplateEdgeCaseTests` (parser-level checks).

| Status | Test name | Description |
|---|---|---|
| Existing | `PrefixAndExplode_ThrowsFormatException` | Mutual exclusion: `{var:3*}` and `{var*:3}` both throw `FormatException`. |
| Existing | `PrefixOnListValue_ThrowsFormatException` | Prefix modifier on a list value throws `FormatException`. |
| Existing | `PrefixOnAssociativeArrayValue_ThrowsFormatException` | Prefix modifier on an associative-array value throws `FormatException`. |
| Existing | `EmptyList_ProducesNoOutput` | Empty `string[]` treated as undefined across multiple operators. |
| Existing | `EmptyAssociativeArray_ProducesNoOutput` | Empty `List<KeyValuePair<string,string>>` treated as undefined across multiple operators. |
| Existing | `SingleMemberList_ExpandsCorrectly` | Single-element list expands correctly for no-explode and explode across operators. |
| Existing | `SinglePairAssociativeArray_NoExplode` | Single-pair associative array, no-explode. |
| Existing | `SinglePairAssociativeArray_Explode` | Single-pair associative array, explode. |
| Existing | `ListWithEmptyStringMembers_QueryExplode` | List with empty-string members applies ifEmp rules for query explode. |
| Existing | `ListWithEmptyStringMembers_SemicolonExplode` | List with empty-string members applies ifEmp rules for semicolon explode. |
| Existing | `PrefixLargerThanLength_ReturnsFullValue` | Prefix larger than value length returns full value. |
| Existing | `PrefixOnEmoji_ReturnsSingleTextElement` | Unicode prefix truncation via code-point counting (`TruncateByCodePoints`). |
| Existing | `MixedLevel1Through4_InOneTemplate` | Mixed Level 1-4 expressions in a single template. |
| Existing | `UnsupportedValueType_Int_ThrowsFormatException` | `int` value throws `FormatException`. |
| Existing | `UnsupportedValueType_Bool_ThrowsFormatException` | `bool` value throws `FormatException`. |
| Existing | `UnsupportedValueType_Object_ThrowsFormatException` | `object` value throws `FormatException`. |
| Existing | `NullValueInObjectDictionary_TreatedAsUndefined` | `null` in `IDictionary<string, object?>` treated as undefined. |
| Existing | `NullValueInObjectDictionary_WrappedByLiterals` | `null` value between literals produces only the literals. |
| Existing | `ListWithNullElement_ThrowsFormatException` | List containing a `null` element throws `FormatException`. |
| Existing | `AssociativeArrayWithNullValue_ThrowsFormatException` | Associative array with a `null` value throws `FormatException`. |
| Existing | `ListOfKeyValuePairs_DispatchesAsAssociativeArray` | `IEnumerable<KeyValuePair<string,string>>` dispatches as assoc-array. |
| Existing | `DictionaryOfStringString_DispatchesAsAssociativeArray` | `IDictionary<string,string>` dispatches as assoc-array. |
| Existing | `StringDictionaryOverload_WorksWithPrefixModifier` | `Expand(IDictionary<string,string>)` works with prefix modifier templates. |
| Existing | `StringDictionaryOverload_WorksWithExplodeModifier` | `Expand(IDictionary<string,string>)` works with explode modifier (string value, no composite). |

## 7. Official URI Template Test Suite

Class: `UriTemplateComplianceTests`

Data-driven harness around https://github.com/uri-templates/uritemplate-test.

| Status | Test group | Expected handling |
|---|---|---|
| Existing | `spec-examples.json` Level 1-4 string/list/dictionary cases | Must pass. |
| Existing | `extended-tests.json` valid Level 1-4 cases | Must pass after parser/encoding gaps are closed. |
| Existing | Invalid grammar cases | Must assert `FormatException` or the chosen diagnostic API behavior. |

## 8. Coverage Summary

Every test class in the suite, mapped to the area it covers. The method count is the
number of `[Fact]`/`[Theory]` methods in the class and is approximate by design: a
`[Theory]` expands to one executed case per data row (the compliance harness in
particular is three theories that fan out over the entire official suite), and the
counts drift as tests are added. Treat them as a size indicator, not a contract.

| Class | Area covered | Plan section | ~Methods |
|---|---|---|---:|
| `UriTemplateLevel1Tests` | Level 1 simple string expansion, literal preservation, encoding edge cases, guard conditions | 1 | 44 |
| `UriTemplateLevel2Tests` | Level 2 reserved (`+`) and fragment (`#`) expansion | 2 | 33 |
| `UriTemplateLevel3Tests` | Level 3 multi-variable and operator expansion (`.`, `/`, `;`, `?`, `&`) | 3 | 65 |
| `UriTemplateGetVariablesTests` | Variable discovery via `GetVariables()` | 4 | 13 |
| `UriTemplateEdgeCaseTests` | Template parsing shape, mixed-level templates, malformed expressions, reserved future operators, Level 4 modifier validation, case sensitivity | 5 | 43 |
| `UriTemplateLevel4Tests` | Level 4 prefix/explode modifiers, list values, associative arrays, Level 4 edge cases | 6 | 73 |
| `UriTemplateComplianceTests` | Data-driven harness over the official `uritemplate-test` suite | 7 | 3 |
| `UriTemplateAssociativeArrayTests` | Associative-array ordering semantics: ordered pair sequences preserve supplied order; unordered containers (dictionaries, sets) are canonicalized to ordinal key order | — | 27 |
| `UriTemplateValueTests` | `UriTemplateValue` factory validation (`From` overloads) and expansion through the `IDictionary<string, UriTemplateValue>` overload | — | 23 |
| `UriTemplateTupleOverloadTests` | The `params (string, object?)[]` `Expand` overload: argument guards, duplicate keys, value-kind dispatch | — | 11 |
| `UriTemplateArgumentContractTests` | Cross-overload argument contract: single-pass enumeration of composite values, lazy failure ordering, null entry/key/element/value diagnostics, set and custom-comparer canonicalization, parser-contract guards | — | 93 |
| `UriTemplateParserValidationTests` | Parser-level literal validation and encoding (ASCII acceptance set, pct-triplets, unpaired surrogates, `ucschar`/`iprivate` scalar sets, apostrophe pass-through), varname dot rules | — | 41 |
| `UriTemplateTypeValidationTests` | Public token-type contracts: `UriTemplateExpressionToken` defensive copying and immutability, `UriTemplateVarSpec` validation, operator strategy resolution | — | 38 |
| `UriTemplateSecurityTests` | Injection/smuggling-focused encoding behavior: reserved pass-through boundaries, control characters, pre-encoded input neutralization, duplicate names, null values | — | 11 |
| `XmlDocExceptionTagShapeTests` | Mechanical enforcement of the `<exception>`-tag shape convention (`docs/development.md` §7) against the core assembly's generated XML documentation: at most one sentence, plus an optional trailing `docs/usage.md` pointer sentence that ends at the path | — | 4 |
| `ServiceCollectionExtensionsTests` (in `Chatter.Rest.UriTemplates.DependencyInjection.Tests`) | `AddUriTemplates` DI registration: service lifetimes, custom parser override, idempotency (not RFC 6570 behavior) | — | 10 |

Classes with `—` in the plan-section column have no dedicated scenario section in this
document; their tests cover API-contract and implementation-invariant behavior that the
RFC-organized sections do not map one-to-one. Individual tests from those classes are
cited from the RFC Review Findings table where they lock a specific RFC behavior.

Both items previously listed here as highest priority are complete:

1. Reserved expansion handling for bare `%` was already correct — `IsReservedChar`
   deliberately excludes `%`, so a bare `%` is encoded as `%25` while a valid
   pct-encoded triplet passes through. The reserved expansion row in the RFC Review
   Findings table records the verified behaviour.
2. Literal validation and encoding was implemented in PR #40: every ASCII character
   outside the Section 2.1 `literals` production that RFC 3986 does not permit in a
   URI is rejected, non-ASCII input is accepted only from the `ucschar` and
   `iprivate` ranges, and unpaired surrogates are rejected. See the literal
   expansion row in the RFC Review Findings table.

One row in the RFC Review Findings table still carries a follow-up, and it is the only one:

- **Variable-name grammar** — a test-coverage gap around uncommon pct-encoded variable names.
  The parser validates them; the edge cases are not exercised.

The previously listed **Undefined null values** follow-up is resolved: the
`IDictionary<string, UriTemplateValue>` overload now treats a null entry as undefined
(issue #21, landed via PR #41), so all overloads agree. See the undefined null values
row in the RFC Review Findings table and
`UriTemplateArgumentContractTests`.

