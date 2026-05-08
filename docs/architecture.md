# Chatter.Rest.UriTemplates — Architecture & Requirements

Spec: https://datatracker.ietf.org/doc/html/rfc6570

---

## Overview

Standalone .NET library implementing RFC 6570 URI Template expansion for Levels 1–4. Ships as NuGet package `Chatter.Rest.UriTemplates`. The core package has no external NuGet dependencies. A companion DI extension package (`Chatter.Rest.UriTemplates.DependencyInjection`) depends on `Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0`.

---

## Requirements

### Functional

- Parse URI template strings containing `{expression}` tokens per RFC 6570 Section 2
- Expand Level 1, Level 2, Level 3, and Level 4 expressions (see [Operator Reference](#operator-reference))
- Return literal text outside expressions unchanged
- Undefined variables (key absent from input dictionary) are omitted per RFC 6570 rules
- Empty-string values are handled per operator semantics (see [Operator Reference](#operator-reference))
- Percent-encode values per RFC 3986 rules appropriate to each operator (see [Encoding Rules](#encoding-rules))
- Support Level 4 prefix modifier (`:N`) for string values and explode modifier (`*`) for list and associative-array values
- Prefix and explode modifiers are mutually exclusive per RFC 6570; the parser enforces this and throws `FormatException`

### Non-Functional

- No external NuGet dependencies
- Target `netstandard2.0` and `net8.0` (matching the rest of the solution)
- Package ID: `Chatter.Rest.UriTemplates`

### Out of Scope

| Feature | Reason |
|---|---|
| URI template composition or merging | Outside RFC 6570 scope |

---

## Solution Structure

```
src/
  Chatter.Rest.UriTemplates/
    Chatter.Rest.UriTemplates.csproj
    UriTemplate.cs               ← public entry point
    UriTemplateValue.cs          ← public strongly-typed value hierarchy
    UriTemplateOperator.cs       ← public operator enum
    UriTemplateToken.cs          ← public token hierarchy (UriTemplateToken, UriTemplateLiteralToken, UriTemplateExpressionToken)
    UriTemplateVarSpec.cs        ← public variable specifier record
    IUriTemplateParser.cs        ← public parser interface
    IUriTemplateFactory.cs       ← public factory interface
    IUriTemplateExpander.cs      ← internal expander interface
    IOperatorStrategy.cs         ← internal strategy interface
    UriTemplateParser.cs         ← internal parser implementation
    UriTemplateExpander.cs       ← internal expander implementation
    UriTemplateFactory.cs        ← internal factory implementation
    UriTemplateEncoder.cs        ← internal percent-encoding utility
    OperatorStrategyFactory.cs   ← internal strategy resolver
    IsExternalInit.cs            ← netstandard2.0 polyfill for init-only setters
    Operators/
      NoneOperatorStrategy.cs    ← Level 1 simple expansion
      PlusOperatorStrategy.cs    ← Level 2 reserved expansion
      HashOperatorStrategy.cs    ← Level 2 fragment expansion
      DotOperatorStrategy.cs     ← Level 3 label expansion
      SlashOperatorStrategy.cs   ← Level 3 path segment expansion
      SemicolonOperatorStrategy.cs ← Level 3 path-style parameter expansion
      QueryOperatorStrategy.cs   ← Level 3 query string expansion
      AmpersandOperatorStrategy.cs ← Level 3 query continuation expansion

  Chatter.Rest.UriTemplates.DependencyInjection/
    Chatter.Rest.UriTemplates.DependencyInjection.csproj
    ServiceCollectionExtensions.cs ← public AddUriTemplates() extension method

test/
  Chatter.Rest.UriTemplates.Tests/
    Chatter.Rest.UriTemplates.Tests.csproj
    UriTemplateLevel1Tests.cs
    UriTemplateLevel2Tests.cs
    UriTemplateLevel3Tests.cs
    UriTemplateLevel4Tests.cs
    UriTemplateEdgeCaseTests.cs
    UriTemplateGetVariablesTests.cs
    UriTemplateValueTests.cs
    UriTemplateTupleOverloadTests.cs
    UriTemplateComplianceTests.cs

  Chatter.Rest.UriTemplates.DependencyInjection.Tests/
    Chatter.Rest.UriTemplates.DependencyInjection.Tests.csproj
    ServiceCollectionExtensionsTests.cs
```

---

## Type Design

### `UriTemplateOperator` (public enum)

```csharp
public enum UriTemplateOperator
{
    None,        // Level 1 — simple string expansion: {var}
    Plus,        // Level 2 — reserved expansion: {+var}
    Hash,        // Level 2 — fragment expansion: {#var}
    Dot,         // Level 3 — label expansion: {.var}
    Slash,       // Level 3 — path segment expansion: {/var}
    Semicolon,   // Level 3 — path-style parameter expansion: {;var}
    Query,       // Level 3 — query string expansion: {?var}
    Ampersand,   // Level 3 — query continuation expansion: {&var}
}
```

### `UriTemplateVarSpec` (public record)

Represents a single variable specifier within an expression, including optional Level 4 modifiers.

```csharp
public sealed record UriTemplateVarSpec(
    string Name,
    int? PrefixLength,
    bool Explode
);
```

- `Name` — the variable name (base name, without modifiers).
- `PrefixLength` — when non-null, the prefix modifier value (1–9999). Applies only to scalar string values; applying a prefix to a composite value (list or associative array) throws `FormatException` at expansion time.
- `Explode` — when `true`, the explode modifier (`*`) is present. Per-operator explode behavior is applied at expansion time.
- Prefix and explode are mutually exclusive. The parser throws `FormatException` if both are present on a single varspec.

### `UriTemplateToken` hierarchy (public)

Parsed URI template tokens form a two-level class hierarchy. The abstract base prevents external subclassing via a `private protected` constructor. The parser emits a sequence of these tokens representing alternating literal text and `{expression}` segments.

```csharp
public abstract class UriTemplateToken
{
    private protected UriTemplateToken() { }
}

public sealed class UriTemplateLiteralToken : UriTemplateToken
{
    public string Value { get; }
    public UriTemplateLiteralToken(string value);
}

public sealed class UriTemplateExpressionToken : UriTemplateToken
{
    public UriTemplateOperator Operator { get; }
    public IReadOnlyList<UriTemplateVarSpec> Variables { get; }
    public UriTemplateExpressionToken(UriTemplateOperator @operator, IReadOnlyList<UriTemplateVarSpec> variables);
}
```

- `UriTemplateLiteralToken` — represents a literal text segment of the template. The `Value` property holds the literal string (validated and encoded by the parser per RFC 6570 section 2.1).
- `UriTemplateExpressionToken` — represents a `{...}` expression segment. Holds the parsed `UriTemplateOperator` and the list of `UriTemplateVarSpec` instances extracted from the expression.

### `IUriTemplateParser` (public interface)

Defines the contract for parsing URI template strings into token sequences.

```csharp
public interface IUriTemplateParser
{
    IReadOnlyList<UriTemplateToken> Parse(string template);
}
```

### `UriTemplateParser` (internal)

Implements `IUriTemplateParser`. Scans a template string left to right, emitting a sequence of `UriTemplateLiteralToken` and `UriTemplateExpressionToken` instances.

```csharp
internal sealed class UriTemplateParser : IUriTemplateParser
{
    internal static readonly UriTemplateParser Default;
    public IReadOnlyList<UriTemplateToken> Parse(string template);
}
```

Responsibilities:
- Detect the operator character immediately after `{` (if any)
- Split comma-separated variable names within the expression
- Validate variable names: RFC 6570 `varname` = `varchar *( ["."] varchar )` where `varchar = ALPHA / DIGIT / "_"` and pct-encoded sequences. Dots and pct-encoded names are uncommon but must not cause a parse error.
- Parse Level 4 modifier syntax: prefix (`:N` where N is 1–9999) and explode (`*` at the end of a varspec). Produces `UriTemplateVarSpec` instances with the appropriate modifier fields set.
- Enforce mutual exclusion of prefix and explode modifiers on a single varspec; throw `FormatException` if both are present.
- Throw `FormatException` for malformed templates (unclosed `{`, nested `{`, invalid modifier syntax)
- Validate and encode literal segments per RFC 6570 section 2.1/3.1 (reject spaces, lone `}`, invalid percent triplets; UTF-8 pct-encode non-ASCII characters)

### `IUriTemplateExpander` (internal interface)

Defines the contract for expanding a single expression token given a variable dictionary.

```csharp
internal interface IUriTemplateExpander
{
    string Expand(UriTemplateExpressionToken expression, IDictionary<string, object?> variables);
}
```

### `UriTemplateExpander` (internal)

Implements `IUriTemplateExpander`. Applies expansion rules for a single `UriTemplateExpressionToken` given a variable dictionary. Delegates per-operator formatting and encoding to `IOperatorStrategy` implementations resolved via `OperatorStrategyFactory`.

```csharp
internal sealed class UriTemplateExpander : IUriTemplateExpander
{
    internal static readonly UriTemplateExpander Default;
    public string Expand(UriTemplateExpressionToken expression, IDictionary<string, object?> variables);
}
```

The expander performs runtime type dispatch on each variable value:

| Runtime type | Dispatch path | Notes |
|---|---|---|
| `string` | Scalar string expansion | Supports prefix truncation via `:N` modifier. |
| `IDictionary<string, string>` | Associative array expansion | Checked before `IEnumerable<string>` to avoid false match. |
| `IEnumerable<KeyValuePair<string, string>>` | Associative array expansion | Preserves insertion order for deterministic output. |
| `IEnumerable<string>` | List expansion | Checked after dictionary/KVP to avoid matching `string` (which is `IEnumerable<char>`). |
| `null` | Treated as undefined | Omitted per RFC 6570 §2.3. |
| Any other type | `FormatException` | Unsupported value type. |

Per-operator expansion algorithm:

1. For each varspec in `expression.Variables`:
   - Look up `varSpec.Name` in the `variables` dictionary. If absent or `null` (undefined): skip entirely.
   - Dispatch by runtime type (see table above).
   - **String values:**
     - If `varSpec.PrefixLength` is set, truncate the value to that many Unicode code points (per RFC 6570 §2.4.1) using the internal `TruncateByCodePoints` method. The method walks the UTF-16 string, pairing valid high+low surrogate pairs as a single code point. This works on both `net8.0` and `netstandard2.0` without a `System.Text.Rune` dependency. Note: combining marks (e.g., `e` + U+0301) count as separate code points, so `{var:1}` on `"é"` keeps only `e`.
     - If the (possibly truncated) value is empty: apply operator-specific empty-value rule (see [Operator Reference](#operator-reference)).
     - If non-empty: encode per operator encoding rule, then format per operator.
   - **List values (non-explode):** encode each member, comma-join into a single composite value. For named operators, prepend `varname=`. Empty list is treated as undefined and omitted.
   - **List values (explode):** each member becomes a separate part. Named operators emit `varname=encodedMember` per member (with ifEmp rules for empty members); non-named operators emit value-only segments.
   - **Associative array values (non-explode):** flatten to alternating `key,value,key,value,...` — each key and value individually encoded, comma-joined. For named operators, prepend `varname=`. Empty associative array is treated as undefined.
   - **Associative array values (explode):** each pair becomes `encodedKey=encodedValue`, joined by operator separator. For named operators with empty values, ifEmp rules apply (`;` omits `=`; `?`/`&` include `=`).
   - **Prefix on composite:** throws `FormatException`. Prefix modifier applies only to scalar string values.
   - **Null elements/values in composites:** throws `FormatException`. List elements and associative array values must be non-null strings.
2. Join the formatted variable results with the operator's separator.
3. Prepend the operator's prefix (if any) to the joined result.
4. If no variables produced output (all undefined or empty composites): return empty string.

### `UriTemplateValue` (public)

A polymorphic hierarchy representing a single URI template variable value. This is the second public type in the library alongside `UriTemplate`. It replaces the need for runtime type dispatch when callers supply composite values.

The hierarchy consists of an abstract base class (`UriTemplateValue`) and three top-level sealed subtypes (`StringValue`, `ListValue`, `DictionaryValue`). Overloaded `From` factory methods on the base class return the specific subtype.

```csharp
public abstract class UriTemplateValue
{
    private protected UriTemplateValue() { }

    public static StringValue     From(string value);
    public static ListValue       From(IEnumerable<string> values);
    public static DictionaryValue From(IDictionary<string, string> pairs);
}

public sealed class StringValue : UriTemplateValue
{
    internal string Value { get; }
}

public sealed class ListValue : UriTemplateValue
{
    internal IReadOnlyList<string> Values { get; }
}

public sealed class DictionaryValue : UriTemplateValue
{
    internal IReadOnlyDictionary<string, string> Pairs { get; }
}
```

- **`From(string)`** — wraps a simple string value. Throws `ArgumentNullException` if `value` is null.
- **`From(IEnumerable<string>)`** — wraps a list of strings (defensively copied to a read-only list). Throws `ArgumentNullException` if `values` is null, `ArgumentException` if any element is null.
- **`From(IDictionary<string, string>)`** — wraps an associative array (defensively copied to a read-only dictionary). Throws `ArgumentNullException` if `pairs` is null, `ArgumentException` if any key or value is null.

The `private protected` constructor prevents external subclassing while allowing the sealed subtypes (`StringValue`, `ListValue`, `DictionaryValue`) to inherit from the base. Instances are created exclusively through the `From` factory methods. All internal properties are immutable snapshots -- the caller's original collection is copied on creation. The subtypes are public, enabling caller-side pattern matching (e.g., C# `switch` expressions) if needed.

**Relationship to `Expand(IDictionary<string, object?>)`:** The existing `object?` overload is preserved for backward compatibility. The new `Expand(IDictionary<string, UriTemplateValue>)` overload provides compile-time safety by eliminating runtime type dispatch on the caller side.

### `UriTemplate` (public)

The public entry point. Parses on construction; expands on demand.

```csharp
public sealed class UriTemplate
{
    public UriTemplate(string template);

    /// <summary>
    /// Expands the URI template with all variables undefined.
    /// Every expression is omitted per RFC 6570 rules.
    /// </summary>
    public string Expand();

    /// <summary>
    /// Expands the URI template using the provided variable dictionary.
    /// All values are treated as simple strings (Levels 1–3 inputs and
    /// string-only Level 4 like {var:3}).
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    public string Expand(IDictionary<string, string> variables);

    /// <summary>
    /// Expands the URI template using the provided variable dictionary,
    /// supporting composite value types for RFC 6570 Level 4 expansion.
    /// Supported value types: string, IEnumerable&lt;string&gt;,
    /// IDictionary&lt;string, string&gt;,
    /// IEnumerable&lt;KeyValuePair&lt;string, string&gt;&gt;, and null.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when a variable value is not a supported type,
    /// when a prefix modifier is applied to a composite value, or when a composite value
    /// contains null elements.</exception>
    public string Expand(IDictionary<string, object?> variables);

    /// <summary>
    /// Expands the URI template using strongly-typed <see cref="UriTemplateValue"/>
    /// instances, supporting all RFC 6570 Level 1–4 value types with compile-time safety.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    public string Expand(IDictionary<string, UriTemplateValue> variables);

    /// <summary>
    /// Expands the URI template using the provided key-value pairs.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    public string Expand(params (string Key, string Value)[] variables);

    /// <summary>
    /// Expands the URI template using the provided variable tuples, supporting composite
    /// value types for RFC 6570 Level 4 expansion.
    /// Delegates to <see cref="Expand(IDictionary{string, object?})"/>.
    /// When duplicate keys are present, the first occurrence wins.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="variables"/> is null.</exception>
    /// <exception cref="FormatException">Thrown when a value is not a supported type.</exception>
    public string Expand(params (string Key, object? Value)[] variables);

    /// <summary>
    /// Returns all variable names referenced in the template, in order of appearance, deduplicated.
    /// For Level 4 varspecs, returns the base name (without :N or * modifiers).
    /// </summary>
    public IReadOnlyList<string> GetVariables();
}
```

### `IOperatorStrategy` / `OperatorStrategyFactory` (internal)

The strategy pattern decouples per-operator expansion logic from the core expander. Each RFC 6570 operator has a dedicated strategy class.

```csharp
internal interface IOperatorStrategy
{
    string Prefix { get; }
    string Separator { get; }
    bool IsNamed { get; }
    string FormatEmpty(string varName);
    string FormatValue(string varName, string encodedValue);
    string Encode(string value);
}
```

- `Prefix` — the string prepended to the entire expression result when at least one variable produces output (e.g., `#` for fragment, `?` for query).
- `Separator` — the string used to join multiple variable results within one expression (e.g., `,`, `.`, `/`, `;`, `&`).
- `IsNamed` — whether the operator emits `varname=value` format (true for `;`, `?`, `&`).
- `FormatEmpty` — applies the operator-specific empty-value rule (e.g., `;varname` without `=` vs `varname=` with `=`).
- `FormatValue` — formats a non-empty encoded value with the variable name where applicable.
- `Encode` — delegates to `UriTemplateEncoder.EncodeUnreserved` or `UriTemplateEncoder.EncodeReserved` as appropriate for the operator.

`OperatorStrategyFactory` is an internal static class that maps `UriTemplateOperator` enum values to singleton `IOperatorStrategy` instances:

```csharp
internal static class OperatorStrategyFactory
{
    internal static IOperatorStrategy For(UriTemplateOperator op);
}
```

Eight strategy classes under `Operators/` implement operator-specific formatting: `NoneOperatorStrategy`, `PlusOperatorStrategy`, `HashOperatorStrategy`, `DotOperatorStrategy`, `SlashOperatorStrategy`, `SemicolonOperatorStrategy`, `QueryOperatorStrategy`, `AmpersandOperatorStrategy`. All are `internal sealed` classes.

### `UriTemplateEncoder` (internal static)

Encapsulates percent-encoding rules per RFC 6570 section 1.6 and RFC 3986.

```csharp
internal static class UriTemplateEncoder
{
    internal static string EncodeUnreserved(string value);
    internal static string EncodeReserved(string value);
}
```

- `EncodeUnreserved` — passes through only unreserved characters (`A-Z a-z 0-9 - . _ ~`) unencoded; percent-encodes all other characters as UTF-8 bytes. Used by Level 1 and Level 3 operators.
- `EncodeReserved` — passes through both unreserved and reserved characters unencoded; preserves existing valid pct-encoded triplets (`%XX`); percent-encodes everything else. Used by Level 2 operators (`+`, `#`).

### `IUriTemplateFactory` / `UriTemplateFactory` (public interface / internal class)

The factory pattern provides a DI-friendly entry point for creating `UriTemplate` instances with injected parser and expander dependencies.

```csharp
public interface IUriTemplateFactory
{
    UriTemplate Create(string template);
}

internal sealed class UriTemplateFactory : IUriTemplateFactory
{
    internal static readonly UriTemplateFactory Default;
    internal UriTemplateFactory(IUriTemplateParser parser, IUriTemplateExpander expander);
    public UriTemplate Create(string template);
}
```

- `IUriTemplateFactory` — public interface exposing `Create(string template)`. Consumers depend on this interface for compile-time safety and testability.
- `UriTemplateFactory` — internal implementation that accepts `IUriTemplateParser` and `IUriTemplateExpander` via constructor injection. Delegates to the internal `UriTemplate(string, IUriTemplateParser, IUriTemplateExpander)` constructor.
- `UriTemplate` also retains its `public UriTemplate(string template)` constructor for direct usage without DI; this constructor uses `UriTemplateParser.Default` and `UriTemplateExpander.Default` internally.

### Dependency Injection (`Chatter.Rest.UriTemplates.DependencyInjection` package)

The companion NuGet package `Chatter.Rest.UriTemplates.DependencyInjection` provides `ServiceCollectionExtensions.AddUriTemplates(IServiceCollection)` for registering URI template services with the Microsoft DI container:

- `IUriTemplateParser` is registered as a **singleton** (via `TryAddSingleton`, using `UriTemplateParser.Default`).
- `IUriTemplateFactory` is registered as **transient** (via `TryAddTransient`), resolving `IUriTemplateParser` from the service provider at each resolution.

The extension method is in the `Microsoft.Extensions.DependencyInjection` namespace following the standard .NET convention. See [docs/usage.md](usage.md) for consumer wiring examples.

---

## Operator Reference

The table below defines behaviour for every supported operator. "Prefix" is prepended to the entire expression result (only when at least one variable produced output). "Separator" joins multiple variable results within one expression. "Encoding" controls which characters are percent-encoded. "Empty value" describes output when a variable is present but is an empty string. "Undefined" describes output when a variable is absent from the input dictionary. "Named" indicates whether the operator emits `varname=value` format.

| Operator | Level | Example | Prefix | Separator | Encoding | Empty value | Undefined | Named |
|---|---|---|---|---|---|---|---|---|
| *(none)* | 1 | `{var}` | — | `,` | unreserved | *(empty string)* | omit | no |
| `+` | 2 | `{+var}` | — | `,` | reserved | *(empty string)* | omit | no |
| `#` | 2 | `{#var}` | `#` | `,` | reserved | `#` | omit / no `#` | no |
| `.` | 3 | `{.var}` | `.` | `.` | unreserved | `.` | omit | no |
| `/` | 3 | `{/var}` | `/` | `/` | unreserved | `/` | omit | no |
| `;` | 3 | `{;var}` | `;` | `;` | unreserved | `;varname` *(no `=`)* | omit | yes |
| `?` | 3 | `{?var}` | `?` | `&` | unreserved | `varname=` *(with `=`)* | omit | yes |
| `&` | 3 | `{&var}` | `&` | `&` | unreserved | `varname=` *(with `=`)* | omit | yes |

**Notes:**
- `#` prefix is emitted only when at least one variable produces a value. If all variables are undefined, the entire expression returns empty string (no lone `#`).
- `.`, `/` prefixes behave the same way — emitted only when output is non-empty.
- `;` empty-value rule: the variable name is included without `=` (e.g. `{;empty}` with `empty=""` → `;empty`).
- `?` and `&` empty-value rule: the variable name is included with `=` but no value (e.g. `{?empty}` with `empty=""` → `?empty=`).

### Level 4 Behavior by Operator

The table below summarizes explode behavior for list and associative-array values across all operators. "Named" operators emit `varname=value` per member when exploding; non-named operators emit value-only segments.

| Operator | Named | List explode | Assoc-array explode | ifEmp (explode, empty member/value) |
|---|---|---|---|---|
| *(none)* | no | `val1,val2,...` | `key1=val1,key2=val2,...` | *(empty string)* |
| `+` | no | `val1,val2,...` | `key1=val1,key2=val2,...` | *(empty string)* |
| `#` | no | `#val1,val2,...` | `#key1=val1,key2=val2,...` | *(empty string)* |
| `.` | no | `.val1.val2....` | `.key1=val1.key2=val2....` | `.` |
| `/` | no | `/val1/val2/...` | `/key1=val1/key2=val2/...` | `/` |
| `;` | yes | `;varname=val1;varname=val2;...` | `;key1=val1;key2=val2;...` | `;varname` (no `=`) / `;key` (no `=`) |
| `?` | yes | `?varname=val1&varname=val2&...` | `?key1=val1&key2=val2&...` | `varname=` / `key=` |
| `&` | yes | `&varname=val1&varname=val2&...` | `&key1=val1&key2=val2&...` | `varname=` / `key=` |

**Prefix modifier (`:N`):**
- Applies only to scalar string values. Truncates the value to `N` Unicode code points (per RFC 6570 §2.4.1) before encoding.
- The implementation walks UTF-16 with surrogate-pair pairing and works on both `net8.0` and `netstandard2.0` without `System.Text.Rune`. Combining marks count as separate code points; a surrogate pair counts as one code point.
- Applying a prefix modifier to a list or associative-array value throws `FormatException`.

**Explode modifier (`*`):**
- For named operators (`;`, `?`, `&`): each list member becomes `varname=encodedMember`; each associative-array pair becomes `encodedKey=encodedValue`. Empty members/values follow the operator's ifEmp rule.
- For non-named operators: each list member becomes a value-only segment; each associative-array pair becomes `encodedKey=encodedValue`.
- Segments are joined by the operator's separator.

**Empty composite values:**
- An empty list (`Count == 0`) or empty associative array is treated as undefined per RFC 6570 §2.3 and produces no output.

---

## Encoding Rules

RFC 6570 defines two encoding strategies:

### Unreserved encoding (Level 1, `.`, `/`, `;`, `?`, `&`)

Pass through only unreserved characters unencoded. Percent-encode everything else as UTF-8 bytes.

Unreserved characters (RFC 3986 Section 2.3):
```
A–Z  a–z  0–9  -  .  _  ~
```

### Reserved encoding (Level 2: `+`, `#`)

Pass through both unreserved characters AND reserved characters unencoded. Percent-encode everything else.

Reserved characters (RFC 3986 Section 2.2 + `%`):
```
:  /  ?  #  [  ]  @  !  $  &  '  (  )  *  +  ,  ;  =  %
```
Pct-encoded sequences (`%XX`) in the source value are also passed through unencoded.

---

## HAL Integration

For integration with HAL `LinkObject`, see the [Chatter.Rest.Hal](https://github.com/brenpike/Chatter.Rest.Hal) repository.

---

## Future Work

See [docs/backlog.md](backlog.md) for deferred follow-ups including a tuple overload for composite values.
