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

Constructor. Parses the template string eagerly on construction, so every parse failure listed below surfaces at construction time — never later, at `Expand`.

```csharp
var template = new UriTemplate("/search{?q,lang}");
```

#### Constructor exceptions

This is the authoritative statement of the constructor's exception contract.

- **`ArgumentNullException`** — `template` is null.
- **`FormatException`** — the template is malformed:
  - an unclosed `{`, a nested `{`, or an empty expression `{}`
  - a double operator, e.g. `{??x}`
  - an invalid variable name: empty, containing whitespace, a leading, trailing, or consecutive dot, an incomplete percent-encoded triplet, or any other character outside the RFC 6570 §2.3 grammar (`ALPHA / DIGIT / "_" / pct-encoded`, optionally separated by single dots)
  - an invalid prefix modifier: `:` followed by nothing, a non-numeric length, a leading zero, or a length outside 1–9999 (RFC 6570 §2.4.1)
  - an explode modifier `*` anywhere but the end of the variable name
  - a prefix modifier and an explode modifier on the same variable — they are mutually exclusive per RFC 6570
  - a literal-validation failure: an ASCII character not permitted in literal text (space, C0 control characters, DEL, `"`, `<`, `>`, `\`, `^`, `` ` ``, `|`, or a bare `{` or `}`), a `%` that does not start a valid percent-encoded triplet, an unpaired UTF-16 surrogate, or a non-ASCII character whose scalar value falls outside the RFC 6570 §1.5 `ucschar`/`iprivate` ranges
- **`NotSupportedException`** — the expression starts with one of the operators RFC 6570 §2.2 reserves for future use: `=`, `,`, `!`, `@`, or `|` (e.g. `{=var}`).

### `IUriTemplateFactory.Create(string template)`

DI alternative to calling `new UriTemplate()` directly. Inject `IUriTemplateFactory` and call `Create` to obtain a `UriTemplate` instance. Available via the `Chatter.Rest.UriTemplates.DependencyInjection` package. See [Section 11 — Dependency Injection](#11-dependency-injection) for setup and usage. With the default parser, `Create` throws exactly what the constructor throws — see [Constructor exceptions](#constructor-exceptions); a custom `IUriTemplateParser` registration substitutes its own parse-failure behavior.

```csharp
UriTemplate template = factory.Create("/orders{?status,page}");
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
- `IDictionary<string, string>` (e.g., `Dictionary<string, string>`) — associative array value, expanded with keys sorted by `string.CompareOrdinal`. An empty dictionary is treated as undefined. See [Associative-array pair order](#associative-array-pair-order).
- `IEnumerable<KeyValuePair<string, string>>` — associative array value. Pair order is determined by the container type: `IDictionary<string, string>` and `ISet<KeyValuePair<string, string>>` are canonicalized (sorted ordinally by key with the value as tie-break); every other enumerable — `List<KeyValuePair<string, string>>`, arrays, `Queue<...>`, iterator methods, LINQ pipelines — is expanded in exactly the order it enumerates, duplicate keys included. An empty sequence is treated as undefined. See [Associative-array pair order](#associative-array-pair-order).

```csharp
var uri = new UriTemplate("{?list*}").Expand(new Dictionary<string, object?>
{
    ["list"] = new[] { "red", "green", "blue" }
});
// Result: "?list=red&list=green&list=blue"
```

The existing `Expand(IDictionary<string, string>)` overload still works for callers who only need string values (Level 1–3 inputs and string-only Level 4 like `{var:3}`).

#### Values must be finite sequences

List and associative-array values carry a caller contract: **every composite value supplied for a variable the template references must be a finite sequence.**

- Within a single `Expand` call, a value the template references is enumerated exactly once, at the first expression that names it, and is fully drained by that first use. The members are recorded and replayed for any later expression naming the same variable, so a single-pass or lazily evaluated sequence is safe within the call.
- Because that first use drains the sequence completely, supplying an endless or never-terminating sequence for a variable the template names makes `Expand` never return.
- Values the template never names are never enumerated at all, so an unused lazy, blocking, or endless sequence alongside the referenced values is harmless.

The same finiteness contract applies to `UriTemplateValue.From(IEnumerable<string>)`, which materializes the sequence eagerly — there, an endless sequence hangs `From` itself rather than `Expand`.

#### Exception message content

Expansion-time `FormatException` messages are written so callers can log them safely:

- Messages identify the failing variable by name — for example, `Variable 'keys' contains a null key. Associative array keys must be non-null strings.` — and, for an associative-array member, the offending key: `Variable 'keys' contains a null value for key 'dot'. Associative array values must be non-null strings.`
- **Variable values never appear in expansion exception messages.** String values, list elements, and associative-array values are never quoted, so the messages stay safe to log even when values carry secrets or personal data. This is a deliberate posture callers can rely on. Associative-array *keys* are the one piece of supplied data that is quoted, as shown above. One encoding-level failure — a value containing an unpaired UTF-16 surrogate — reports the character index within the value instead of the variable name; it likewise contains no value text.
- `UriTemplateValue.From` follows the same posture at construction time: its `ArgumentException` messages name the offending key (`Dictionary must not contain null values (key: 'dot').`), never the value.

This posture is scoped to expansion-time messages about variable values. Parse-time `FormatException` messages from the [constructor](#constructor-exceptions) deliberately quote template text — variable names and offending literal characters — because there the template itself is the malformed input.

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

RFC 6570 permits duplicate variable names, and each occurrence expands: `{?x,x}` with `x = "1"` produces `?x=1&x=1`, while `GetVariables()` reports `x` once. Callers that count expansion output via `GetVariables()` will under-count in that case.

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

**Only expand trusted values with `{+var}` and `{#var}`.** Both operators bypass reserved-character encoding — exactly what RFC 6570 Section 3.2.3 requires — so the value can change the meaning of the surrounding URI. Which component it can reach depends on where the expression sits in the template.

With `http://ex.com/a{+p}` the expression sits in the path, after the authority has already ended, so the value can add or rewrite everything from the path onward:

| Value | Expansion | Effect |
|---|---|---|
| `?admin=1` | `http://ex.com/a?admin=1` | starts the query string |
| `#frag` | `http://ex.com/a#frag` | starts the fragment |
| `../../etc/passwd` | `http://ex.com/a../../etc/passwd` | traversal sequence passes through raw |
| `x@evil.com` | `http://ex.com/ax@evil.com` | stays in the path — no userinfo is introduced from this position |
| `//evil.com/a` | `http://ex.com/a//evil.com/a` | stays in the path — no authority is rewritten from this position |

When the expression sits inside or before the authority, the value reaches the host itself:

| Template | Value | Expansion |
|---|---|---|
| `http://{+host}/path` | `x@evil.com` | `http://x@evil.com/path` — a userinfo component is introduced and the request moves to another host |
| `http://{+host}/path` | `evil.com` | `http://evil.com/path` |
| `{+p}/path` | `//evil.com` | `//evil.com/path` — a scheme-relative URL pointing at another origin |

Under the default `{var}` operator these characters are percent-encoded, so none of them can change the URI's structure: `http://{host}/path` with `host = "x@evil.com"` produces `http://x%40evil.com/path`, and `/proxy/{path}` with `path = "../../etc/passwd"` produces `/proxy/..%2F..%2Fetc%2Fpasswd`. Use the default operator for untrusted values; if reserved expansion is genuinely required, validate the value against a caller-side allowlist first. See [Encoding Rules](#6-encoding-rules) for how pre-encoded sequences behave under `{+}` and `{#}`.

**What the default operator does and does not guarantee.** Percent-encoding guarantees that the value cannot alter the structure of the URI as parsed — the encoded value stays inside the single component it was expanded into. It does not sanitize the value's meaning. `/proxy/..%2F..%2Fetc%2Fpasswd` decodes straight back to `../../etc/passwd`, so any downstream component that percent-decodes before routing or filesystem normalization sees the traversal sequence again. Encoding defers that problem to the consumer; it does not eliminate it. Validate or normalize untrusted path values on the receiving side regardless of which operator produced them.

### Level 2 — Fragment Expansion `{#var}`

Prepends `#` to the expanded value:

```csharp
var t = new UriTemplate("/page{#section}");
t.Expand(("section", "overview"));
// Result: "/page#overview"
```

`{#var}` uses the same reserved encoding as `{+var}`, so the trust warning above applies equally here.

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

Note: a template that begins with `{/...}` can produce a scheme-relative URL when the first variable expands to an empty string — `{/a,b}` with `a = ""` and `b = "evil.com"` produces `//evil.com`. This is RFC-conformant, but if such a template's values are not trusted, prefix the template with a literal path segment.

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

Reserved characters: `: / ? # [ ] @ ! $ & ' ( ) * + , ; =`

```csharp
var t = new UriTemplate("{+path}");
t.Expand(("path", "/foo/bar?q=1"));
// Result: "/foo/bar?q=1"   (slashes, ?, = all preserved)
```

`%` itself is not in the pass-through set: under `{+}` and `{#}` a valid percent triplet (`%` followed by two hex digits) is preserved verbatim, while a bare `%` is encoded as `%25`.

**Trust boundary.** Preserving pre-encoded triplets is part of the `{+}`/`{#}` trust boundary: `{+p}` with `p = "%0d%0aX: y"` yields `%0d%0aX:%20y`, and `p = "%2e%2e%2fetc"` stays `%2e%2e%2fetc` — a downstream server that decodes these sees control characters or a traversal sequence. Unreserved encoding neutralizes the same input by encoding `%` as `%25`, so this is the key behavioral difference between the two operator families: values expanded with `{+}` or `{#}` must be trusted.

Even under `{+}` and `{#}`, characters outside the reserved and unreserved sets are always percent-encoded: raw CR, LF, NUL, backslash, and direction-override characters such as U+202E never pass through (`"a\r\nb"` becomes `a%0D%0Ab`), and non-ASCII text is always UTF-8 percent-encoded, so raw homograph bytes never appear in the output.

That guarantee is scoped to *raw* control characters in the value, and it is not an end-to-end guarantee against HTTP header splitting. As the trust-boundary note above states, `{+}` and `{#}` preserve valid percent triplets, so a caller-supplied `%0d%0a` survives expansion unchanged (`{+p}` with `p = "%0d%0aX: y"` yields `%0d%0aX:%20y`) and becomes CR LF again in any consumer that percent-decodes the value before placing it in a header. What this library guarantees is that it never *introduces* a raw control character into the output; whether a decoded value is safe in a header, a host position, or a filesystem path must be validated where that decoding happens.

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

### Associative-array pair order

RFC 6570 mandates no particular pair order for associative-array values, so this library defines one. The order an expansion produces is derived from the ordering contract of the value the caller supplies:

| Supplied value | Pair order |
|---|---|
| A keyed or set container: `IDictionary<string, string>` (`Dictionary`, `FrozenDictionary`, `ImmutableDictionary`, `ConcurrentDictionary`, `ReadOnlyDictionary`, `SortedDictionary`, `SortedList`), including `UriTemplateValue.From(IDictionary<string, string>)`, or `ISet<KeyValuePair<string, string>>` (`HashSet`, `FrozenSet`, `ImmutableHashSet`) | Canonicalized: sorted ordinally by key (`string.CompareOrdinal`), with an ordinal comparison of the value as tie-break |
| Every other `IEnumerable<KeyValuePair<string, string>>` — `List<KeyValuePair<string, string>>`, arrays, `ImmutableArray<...>`, `ImmutableList<...>`, `ReadOnlyCollection<...>`, `Queue<...>`, `LinkedList<...>`, `Stack<...>`, iterator methods, LINQ pipelines such as `Select` and `OrderBy`, and custom enumerables | Exactly the order the sequence enumerates, preserved verbatim, duplicate keys included |

Canonicalization is a uniform policy applied to these two interfaces, not an inference about each container. Most keyed and set containers genuinely expose no way to place one pair before another — a `Dictionary<string, string>` enumerates in hash-slot order, which diverges from insertion order once an entry is removed and another inserted into the freed slot. A few do carry a caller-supplied order: `SortedDictionary`, `SortedList`, and `SortedSet` enumerate by their comparer, and the library replaces that order with its own. Sorting every implementation of these interfaces the same way means one interface always implies one ordering, and makes the expansion reproducible. The value tie-break exists because a set can hold two pairs with the same key; dictionary keys are unique, so for a dictionary the tie-break never fires and the order is purely ordinal by key.

No .NET interface distinguishes an ordered sequence from an unordered one — a `Queue<T>` and a `HashSet<T>` are both just `IEnumerable<T>` — so the library does not guess: it canonicalizes only where the container type proves the order is not yours, and defers to you everywhere else. If you supply a custom container that enumerates nondeterministically but implements neither `IDictionary<string, string>` nor `ISet<KeyValuePair<string, string>>`, determinism is yours to impose: **supply an ordered sequence — a `List<KeyValuePair<string, string>>` or an `OrderBy` projection — or convert it to an `IDictionary<string, string>` and let the canonical order apply.** Conversely, a sequence you deliberately ordered — including one that repeats a key — is never reordered.

Two edge cases follow directly from the type tests. `SortedSet<KeyValuePair<string, string>>` and `ImmutableSortedSet<KeyValuePair<string, string>>` implement `ISet<...>`, so they are canonicalized even though a comparer orders them (a rare shape — populating one with more than a single distinct pair requires an explicit `IComparer<KeyValuePair<string, string>>`, since `KeyValuePair<,>` is not comparable and the default comparer throws on the first comparison; constructing an empty one, or adding a single pair, succeeds without a comparer). And a custom type that implements only `IReadOnlyDictionary<string, string>` without `IDictionary<string, string>` falls into the preserve branch and is expanded in its enumeration order. `FrozenDictionary<string, string>` implements `IDictionary<string, string>` and `FrozenSet<KeyValuePair<string, string>>` implements `ISet<...>`, so both are canonicalized.

The sort is ordinal, not culture-aware, so uppercase ASCII sorts before lowercase:

```csharp
var uri = new UriTemplate("{?o*}").Expand(new Dictionary<string, object?>
{
    ["o"] = new Dictionary<string, string>
    {
        ["a"] = "1",
        ["_"] = "2",
        ["Z"] = "3",
        ["B"] = "4",
    }
});
// Result: "?B=4&Z=3&_=2&a=1"
```

#### Associative array via `Dictionary<string, string>`

Keys are sorted ordinally regardless of the order they were added in:

```csharp
var uri = new UriTemplate("{?keys*}").Expand(new Dictionary<string, object?>
{
    ["keys"] = new Dictionary<string, string>
    {
        ["semi"] = ";",
        ["dot"] = ".",
        ["comma"] = ",",
    }
});
// Result: "?comma=%2C&dot=.&semi=%3B"
```

#### Associative array via `List<KeyValuePair<string, string>>`

Use `List<KeyValuePair<string, string>>` to choose the pair order yourself:

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

var uri2 = new UriTemplate("{?keys*}").Expand(new Dictionary<string, object?>
{
    ["keys"] = keys
});
// Result: "?semi=%3B&dot=.&comma=%2C"
```

An ordered sequence is also the only way to expand duplicate keys, which a dictionary cannot represent:

```csharp
var tags = new List<KeyValuePair<string, string>>
{
    new("tag", "a"),
    new("tag", "b"),
};

var uri = new UriTemplate("{?tags*}").Expand(new Dictionary<string, object?>
{
    ["tags"] = tags
});
// Result: "?tag=a&tag=b"
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
        ["comma"] = ",",
    })
});
// Result: "?comma=%2C&dot=.&semi=%3B"
```

`UriTemplateValue.From(IDictionary<string, string>)` follows the same rule as a plain `IDictionary<string, string>`: keys are sorted by `string.CompareOrdinal`. To choose the pair order, supply a `List<KeyValuePair<string, string>>` through the `IDictionary<string, object?>` overload instead — see [Associative-array pair order](#associative-array-pair-order).

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

---

## 11. Dependency Injection

The `Chatter.Rest.UriTemplates.DependencyInjection` package provides integration with `Microsoft.Extensions.DependencyInjection`.

### Installation

```bash
dotnet add package Chatter.Rest.UriTemplates.DependencyInjection
```

### Service Registration

Call `AddUriTemplates()` on your `IServiceCollection` during startup:

```csharp
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddUriTemplates();
```

This registers:

- `IUriTemplateParser` as a **singleton** — the stateless parser is shared across the application.
- `IUriTemplateFactory` as **transient** — a new factory instance is resolved each time, using the parser from the current scope.

Both registrations use `TryAdd`, so they will not override any custom registrations you have already added to the container.

### Using `IUriTemplateFactory`

Inject `IUriTemplateFactory` and call `Create` to build `UriTemplate` instances:

```csharp
using Chatter.Rest.UriTemplates;

public class OrderClient
{
    private readonly IUriTemplateFactory _templateFactory;

    public OrderClient(IUriTemplateFactory templateFactory)
    {
        _templateFactory = templateFactory;
    }

    public string BuildOrderUri(string status, string page)
    {
        var template = _templateFactory.Create("/orders{?status,page}");
        return template.Expand(("status", status), ("page", page));
    }
}
```

`IUriTemplateFactory.Create(string template)` returns a `UriTemplate` instance with the same behavior as `new UriTemplate(string)` — the same `Expand` overloads, `GetVariables()`, and encoding rules apply. Use the factory when you want to avoid a direct dependency on the `UriTemplate` constructor for testability or when the parser implementation is provided through the container. Note: `IUriTemplateExpander` is internal and not registered in the DI container — the expander cannot be replaced via DI.
