# URI Templates Usage Guide

Practical usage guide for `Chatter.Rest.UriTemplates`.

---

## 1. Overview

`Chatter.Rest.UriTemplates` is a standalone RFC 6570 URI Template expansion library for .NET. It supports **Levels 1 through 4** of the RFC 6570 specification, covering simple string expansion, reserved/fragment expansion, all Level 3 operators (label, path segment, path-style parameter, form-style query, and form-style query continuation), and Level 4 value modifiers (prefix `:N` and explode `*`) with composite value types (lists and associative arrays).

**Spec:** [RFC 6570 — URI Template](https://datatracker.ietf.org/doc/html/rfc6570)

---

## 2. Installation

```bash
dotnet add package Chatter.Rest.UriTemplates
```

**Namespace:**

```csharp
using Chatter.Rest.UriTemplates;
```

The package targets `netstandard2.0` and `net8.0` with no external dependencies.

---

## 3. Quick Start

```csharp
using Chatter.Rest.UriTemplates;

var template = new UriTemplate("/orders{?status,page}");

var uri = template.Expand(new Dictionary<string, string>
{
    ["status"] = "shipped",
    ["page"] = "2"
});
// Result: "/orders?status=shipped&page=2"
```

---

## 4. API Reference

### `UriTemplate(string template)`

Constructor. Parses the template string eagerly on construction.

- Throws `ArgumentNullException` if `template` is null.
- Throws `FormatException` for malformed templates (unclosed `{`, nested `{`, invalid modifier syntax, mutually exclusive prefix and explode modifiers).

```csharp
var template = new UriTemplate("/search{?q,lang}");
```

### `string Expand(IDictionary<string, string> variables)`

Expands the URI template using the provided variable dictionary.

- Throws `ArgumentNullException` if `variables` is null.
- Variables absent from the dictionary are treated as undefined and omitted per RFC 6570 rules.

```csharp
var uri = template.Expand(new Dictionary<string, string>
{
    ["q"] = "dotnet",
    ["lang"] = "en"
});
// Result: "/search?q=dotnet&lang=en"
```

### `string Expand(IDictionary<string, object?> variables)`

Expands the URI template using a dictionary that supports composite value types for RFC 6570 Level 4 expansion.

- **Parameter:** `variables` — a dictionary mapping variable names to values.
- **Returns:** the expanded URI string.
- **Throws `ArgumentNullException`** if `variables` is null.
- **Throws `FormatException`** if a variable value is not a supported type, if a prefix modifier is applied to a composite value (list or associative array), or if a composite value contains null elements.

Supported value types:
- `null` — treated as undefined (variable is omitted).
- `string` — simple string value. Works with all operators and Level 4 prefix modifiers.
- `IEnumerable<string>` (e.g., `string[]`, `List<string>`) — list value. An empty list is treated as undefined.
- `IDictionary<string, string>` (e.g., `Dictionary<string, string>`) — associative array value. An empty dictionary is treated as undefined.
- `IEnumerable<KeyValuePair<string, string>>` (e.g., `List<KeyValuePair<string, string>>`) — associative array value with deterministic insertion order. An empty sequence is treated as undefined.

```csharp
var uri = new UriTemplate("{?list*}").Expand(new Dictionary<string, object?>
{
    ["list"] = new[] { "red", "green", "blue" }
});
// Result: "?list=red&list=green&list=blue"
```

The existing `Expand(IDictionary<string, string>)` overload still works for callers who only need string values (Level 1–3 inputs and string-only Level 4 like `{var:3}`).

### `string Expand(params (string Key, string Value)[] variables)`

Tuple convenience overload. First-wins for duplicate keys.

- Throws `ArgumentNullException` if `variables` is null.

```csharp
var uri = template.Expand(
    ("q", "dotnet"),
    ("lang", "en")
);
// Result: "/search?q=dotnet&lang=en"
```

Duplicate handling:

```csharp
var uri = template.Expand(
    ("q", "first"),
    ("q", "second")    // ignored — "first" wins
);
// Result: "/search?q=first"
```

### `string Expand(params (string Key, object? Value)[] variables)`

Tuple convenience overload for composite values. Accepts string, list, dictionary, and null values via `object?`. First-wins for duplicate keys. Delegates to the canonical `IDictionary<string, object?>` expansion path.

- Throws `ArgumentNullException` if `variables` is null.
- Throws `FormatException` if a value is not a supported type (including `UriTemplateValue` -- use the dedicated overload instead), if a prefix modifier is applied to a composite value, or if a composite value contains null elements.

```csharp
var uri = new UriTemplate("/users/{id}{?tag*}").Expand(
    ("id", (object?)"42"),
    ("tag", (object?)new[] { "active", "premium" })
);
// Result: "/users/42?tag=active&tag=premium"
```

Mixed-type usage with string, list, and dictionary values in one call:

```csharp
var uri = new UriTemplate("{/path}{?color*}{;keys*}").Expand(
    ("path", (object?)"files"),
    ("color", (object?)new[] { "red", "green" }),
    ("keys", (object?)new List<KeyValuePair<string, string>>
    {
        new("semi", ";"),
        new("dot", "."),
    })
);
// Result: "/files?color=red&color=green;semi=%3B;dot=."
```

Duplicate handling:

```csharp
var uri = new UriTemplate("/users/{id}").Expand(
    ("id", (object?)"first"),
    ("id", (object?)"second")    // ignored -- "first" wins
);
// Result: "/users/first"
```

> **Note:** Passing a `UriTemplateValue` instance through this overload throws `FormatException`. Use `Expand(IDictionary<string, UriTemplateValue>)` for strongly-typed values.

### `string Expand()`

Expands the URI template with all variables undefined. Every expression is omitted per RFC 6570 rules — equivalent to passing an empty dictionary.

```csharp
var uri = new UriTemplate("/orders{?status,page}").Expand();
// Result: "/orders"
```

### `IReadOnlyList<string> GetVariables()`

Returns all variable names referenced in the template, deduplicated, in order of first appearance.

```csharp
var template = new UriTemplate("/orders{?status,page}{&lang}");
var vars = template.GetVariables();
// Result: ["status", "page", "lang"]
```

---

## 5. Operator Examples

### Level 1 — Simple String Expansion `{var}`

```csharp
var t = new UriTemplate("/users/{id}");
t.Expand(("id", "42"));
// Result: "/users/42"
```

Values are percent-encoded using unreserved encoding:

```csharp
var t = new UriTemplate("/search/{query}");
t.Expand(("query", "hello world"));
// Result: "/search/hello%20world"
```

### Level 2 — Reserved Expansion `{+var}`

Reserved characters in the value are passed through unencoded:

```csharp
var t = new UriTemplate("/proxy/{+path}");
t.Expand(("path", "foo/bar/baz"));
// Result: "/proxy/foo/bar/baz"
```

### Level 2 — Fragment Expansion `{#var}`

Prepends `#` to the expanded value:

```csharp
var t = new UriTemplate("/page{#section}");
t.Expand(("section", "overview"));
// Result: "/page#overview"
```

### Level 3 — Label Expansion `{.var}`

Prepends `.` and uses `.` as separator:

```csharp
var t = new UriTemplate("/api{.version}");
t.Expand(("version", "v2"));
// Result: "/api.v2"

var t2 = new UriTemplate("/host{.sub,domain}");
t2.Expand(("sub", "www"), ("domain", "example"));
// Result: "/host.www.example"
```

### Level 3 — Path Segment Expansion `{/var}`

Prepends `/` and uses `/` as separator:

```csharp
var t = new UriTemplate("/files{/dir,file}");
t.Expand(("dir", "photos"), ("file", "cat.jpg"));
// Result: "/files/photos/cat.jpg"
```

### Level 3 — Path-Style Parameter Expansion `{;var}`

Prepends `;` and uses `;` as separator. Empty values include the name without `=`:

```csharp
var t = new UriTemplate("/matrix{;x,y}");
t.Expand(("x", "1"), ("y", "2"));
// Result: "/matrix;x=1;y=2"

// Empty value:
t.Expand(("x", ""), ("y", "2"));
// Result: "/matrix;x;y=2"
```

### Level 3 — Form-Style Query Expansion `{?var}`

Prepends `?` and uses `&` as separator. Empty values include `=`:

```csharp
var t = new UriTemplate("/orders{?status,page}");
t.Expand(("status", "shipped"), ("page", "2"));
// Result: "/orders?status=shipped&page=2"

// Empty value:
t.Expand(("status", ""));
// Result: "/orders?status="
```

### Level 3 — Form-Style Query Continuation `{&var}`

Prepends `&` and uses `&` as separator. Use this for appending to an existing query string:

```csharp
var t = new UriTemplate("/orders?mode=list{&status,page}");
t.Expand(("status", "shipped"), ("page", "2"));
// Result: "/orders?mode=list&status=shipped&page=2"
```

---

## 6. Encoding Rules

The library applies two encoding strategies per RFC 6570:

**Unreserved encoding** (Level 1, `.`, `/`, `;`, `?`, `&` operators):
Only unreserved characters pass through unencoded. Everything else is percent-encoded as UTF-8 bytes.

Unreserved characters: `A-Z a-z 0-9 - . _ ~`

```csharp
var t = new UriTemplate("/search/{query}");
t.Expand(("query", "hello world!"));
// Result: "/search/hello%20world%21"
```

**Reserved encoding** (Level 2: `+` and `#` operators):
Both unreserved and reserved characters pass through unencoded. Only characters outside both sets are percent-encoded.

Reserved characters: `: / ? # [ ] @ ! $ & ' ( ) * + , ; = %`

```csharp
var t = new UriTemplate("{+path}");
t.Expand(("path", "/foo/bar?q=1"));
// Result: "/foo/bar?q=1"   (slashes, ?, = all preserved)
```

---

## 7. LinkObject Integration

For integration with the HAL `LinkObject`, see the [Chatter.Rest.Hal](https://github.com/brenpike/Chatter.Rest.Hal) package.

---

## 8. Undefined Variables

When a variable referenced in the template is absent from the provided dictionary, it is treated as **undefined** and omitted entirely per RFC 6570 rules. No placeholder or literal `{var}` text is left in the output.

```csharp
var t = new UriTemplate("/orders{?status,page}");

// Only "status" provided; "page" is undefined:
t.Expand(("status", "shipped"));
// Result: "/orders?status=shipped"

// All variables undefined:
t.Expand(new Dictionary<string, string>());
// Result: "/orders"
```

This applies consistently across all operator types. Operator prefixes (`?`, `#`, `.`, `/`, `;`, `&`) are only emitted when at least one variable in the expression produces a value.

---

## 9. Level 4 Examples

RFC 6570 Level 4 adds two value modifiers and composite value types:

- **Prefix** (`{var:3}`) — truncate a string value to a maximum number of Unicode code points before expansion (per RFC 6570 §2.4.1). Surrogate pairs count as one code point; combining marks (e.g., `e` + U+0301) count as separate code points.
- **Explode** (`{var*}`) — expand list or associative array values into separate segments per the operator's rules.

Level 4 expansion requires the `Expand(IDictionary<string, object?>)` overload so that list and associative-array values can be supplied alongside string values.

### Prefix modifier on a string

```csharp
var uri = new UriTemplate("{var:3}").Expand(new Dictionary<string, object?>
{
    ["var"] = "value"
});
// Result: "val"
```

### List value

Pass a list as any `IEnumerable<string>` (e.g., `string[]` or `List<string>`):

```csharp
var uri = new UriTemplate("{?list}").Expand(new Dictionary<string, object?>
{
    ["list"] = new[] { "red", "green", "blue" }
});
// Result: "?list=red,green,blue"
```

### Associative array via `List<KeyValuePair<string, string>>`

Use `List<KeyValuePair<string, string>>` for deterministic enumeration order:

```csharp
var keys = new List<KeyValuePair<string, string>>
{
    new("semi", ";"),
    new("dot", "."),
    new("comma", ","),
};

var uri = new UriTemplate("{keys}").Expand(new Dictionary<string, object?>
{
    ["keys"] = keys
});
// Result: "semi,%3B,dot,.,comma,%2C"
```

### Associative array via `Dictionary<string, string>`

A `Dictionary<string, string>` also works but note that enumeration order may vary:

```csharp
var uri = new UriTemplate("{?keys*}").Expand(new Dictionary<string, object?>
{
    ["keys"] = new Dictionary<string, string>
    {
        ["semi"] = ";",
        ["dot"] = ".",
    }
});
// Result order may vary, e.g.: "?semi=%3B&dot=." or "?dot=.&semi=%3B"
```

### Explode for path-style (`;`), query (`?`), and ampersand (`&`)

```csharp
var vars = new Dictionary<string, object?>
{
    ["list"] = new[] { "red", "green", "blue" }
};

// Semicolon explode: each member becomes ;varname=value
new UriTemplate("{;list*}").Expand(vars);
// Result: ";list=red;list=green;list=blue"

// Query explode: each member becomes varname=value, joined by &
new UriTemplate("{?list*}").Expand(vars);
// Result: "?list=red&list=green&list=blue"

// Ampersand explode: same as query but with & prefix
new UriTemplate("{&list*}").Expand(vars);
// Result: "&list=red&list=green&list=blue"
```

### Mixed-level template

Level 1–3 expressions and Level 4 expressions can coexist in a single template:

```csharp
var uri = new UriTemplate("/users/{id}{?filter*}").Expand(new Dictionary<string, object?>
{
    ["id"] = "42",
    ["filter"] = new[] { "active", "premium" }
});
// Result: "/users/42?filter=active&filter=premium"
```

---

## 10. Strongly-Typed Values with `UriTemplateValue`

`UriTemplateValue` is a strongly-typed alternative to the `IDictionary<string, object?>` overload. Instead of relying on runtime type dispatch, callers create values through overloaded `From` factory methods and get compile-time safety.

### API Reference

```csharp
public abstract class UriTemplateValue
{
    private protected UriTemplateValue() { }

    public static StringValue     From(string value);
    public static ListValue       From(IEnumerable<string> values);
    public static DictionaryValue From(IDictionary<string, string> pairs);
}

public sealed class StringValue : UriTemplateValue { internal string Value { get; } }
public sealed class ListValue : UriTemplateValue { internal IReadOnlyList<string> Values { get; } }
public sealed class DictionaryValue : UriTemplateValue { internal IReadOnlyDictionary<string, string> Pairs { get; } }
```

The corresponding `Expand` overload:

```csharp
public string Expand(IDictionary<string, UriTemplateValue> variables);
```

### String value

```csharp
var uri = new UriTemplate("/users/{id}").Expand(new Dictionary<string, UriTemplateValue>
{
    ["id"] = UriTemplateValue.From("42")
});
// Result: "/users/42"
```

### List value

```csharp
var uri = new UriTemplate("{?color*}").Expand(new Dictionary<string, UriTemplateValue>
{
    ["color"] = UriTemplateValue.From(new[] { "red", "green", "blue" })
});
// Result: "?color=red&color=green&color=blue"
```

### Dictionary value

```csharp
var uri = new UriTemplate("{?keys*}").Expand(new Dictionary<string, UriTemplateValue>
{
    ["keys"] = UriTemplateValue.From(new Dictionary<string, string>
    {
        ["semi"] = ";",
        ["dot"] = ".",
    })
});
// Result order may vary, e.g.: "?semi=%3B&dot=." or "?dot=.&semi=%3B"
```

### Mixed value kinds in one call

```csharp
var uri = new UriTemplate("/users/{id}{?tag*}").Expand(new Dictionary<string, UriTemplateValue>
{
    ["id"] = UriTemplateValue.From("42"),
    ["tag"] = UriTemplateValue.From(new[] { "active", "premium" })
});
// Result: "/users/42?tag=active&tag=premium"
```

### When to use each overload

| Overload | Best for |
|---|---|
| `Expand()` | All variables undefined — expands the template with no values. |
| `Expand(IDictionary<string, string>)` | Simple string-only values (Levels 1-3 and string-only Level 4). |
| `Expand(IDictionary<string, object?>)` | Mixed value types when working with loosely-typed data via dictionary. |
| `Expand(IDictionary<string, UriTemplateValue>)` | Mixed value types with compile-time safety via dictionary. |
| `Expand(params (string, string)[])` | Quick inline calls with string-only values. |
| `Expand(params (string, object?)[])` | Quick inline calls with mixed value types (string, list, dict). |

The `object?` and `UriTemplateValue` overloads (both dictionary and tuple forms) produce identical expansion results. Choose `UriTemplateValue` when you want the compiler to catch invalid value types instead of getting a `FormatException` at runtime. The tuple overloads delegate to their dictionary counterparts, adding only first-wins duplicate handling.
