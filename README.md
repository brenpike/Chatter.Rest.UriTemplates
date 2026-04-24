# Chatter.Rest.UriTemplates

> RFC 6570 URI Template expansion for .NET (Levels 1-3)

[![CI](https://github.com/brenpike/Chatter.Rest.UriTemplates/actions/workflows/uritemplate-cicd.yml/badge.svg)](https://github.com/brenpike/Chatter.Rest.UriTemplates/actions/workflows/uritemplate-cicd.yml)
[![NuGet](https://img.shields.io/nuget/v/Chatter.Rest.UriTemplates?label=Chatter.Rest.UriTemplates)](https://www.nuget.org/packages/Chatter.Rest.UriTemplates)

## Features

- RFC 6570 URI Template expansion, Levels 1-3
- All 8 operators: `{var}`, `{+var}`, `{#var}`, `{.var}`, `{/var}`, `{;var}`, `{?var}`, `{&var}`
- Zero external NuGet dependencies
- Targets `net8.0` and `netstandard2.0`
- Percent-encoding per RFC 3986 (unreserved and reserved strategies)
- Undefined variables omitted per RFC 6570 rules
- Level 4 modifiers (`:N` prefix, `*` explode) detected and throw `NotSupportedException`

## Table of Contents

- [Quick Start](#quick-start)
- [Installation](#installation)
- [API Reference](#api-reference)
- [Operator Reference](#operator-reference)
- [Encoding](#encoding)
- [Undefined Variables](#undefined-variables)
- [Level 4 Not Supported](#level-4-not-supported)
- [Additional Resources](#additional-resources)
- [License](#license)
- [Contributing](#contributing)

## Quick Start

```bash
dotnet add package Chatter.Rest.UriTemplates
```

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

## Installation

```bash
dotnet add package Chatter.Rest.UriTemplates
```

**Namespace:**

```csharp
using Chatter.Rest.UriTemplates;
```

Targets `netstandard2.0` and `net8.0` with no external dependencies.

## API Reference

### `UriTemplate(string template)`

Constructor. Parses the template string eagerly on construction.

- Throws `ArgumentNullException` if `template` is null.
- Throws `FormatException` for malformed templates (unclosed `{`, nested `{`, empty `{}`).
- Throws `NotSupportedException` for Level 4 modifier syntax (`:N` prefix, `*` explode).

```csharp
var template = new UriTemplate("/search{?q,lang}");
```

### `string Expand(IDictionary<string, string> variables)`

Expands the URI template using the provided variable dictionary. Variables absent from the dictionary are treated as undefined and omitted per RFC 6570 rules.

- Throws `ArgumentNullException` if `variables` is null.

```csharp
var uri = template.Expand(new Dictionary<string, string>
{
    ["q"] = "dotnet",
    ["lang"] = "en"
});
// Result: "/search?q=dotnet&lang=en"
```

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
    ("q", "second")    // ignored -- "first" wins
);
// Result: "/search?q=first"
```

### `IReadOnlyList<string> GetVariables()`

Returns all variable names referenced in the template, deduplicated, in order of first appearance.

```csharp
var template = new UriTemplate("/orders{?status,page}{&lang}");
var vars = template.GetVariables();
// Result: ["status", "page", "lang"]
```

## Operator Reference

| Operator | Level | Example | Prefix | Separator | Encoding |
|---|---|---|---|---|---|
| *(none)* | 1 | `{var}` | -- | `,` | unreserved |
| `+` | 2 | `{+var}` | -- | `,` | reserved |
| `#` | 2 | `{#var}` | `#` | `,` | reserved |
| `.` | 3 | `{.var}` | `.` | `.` | unreserved |
| `/` | 3 | `{/var}` | `/` | `/` | unreserved |
| `;` | 3 | `{;var}` | `;` | `;` | unreserved |
| `?` | 3 | `{?var}` | `?` | `&` | unreserved |
| `&` | 3 | `{&var}` | `&` | `&` | unreserved |

**Simple expansion** -- values are percent-encoded using unreserved encoding:

```csharp
var t = new UriTemplate("/users/{id}");
t.Expand(("id", "42"));
// Result: "/users/42"
```

**Reserved expansion** -- reserved characters pass through unencoded:

```csharp
var t = new UriTemplate("/proxy/{+path}");
t.Expand(("path", "foo/bar/baz"));
// Result: "/proxy/foo/bar/baz"
```

**Form-style query** -- prepends `?` and joins with `&`:

```csharp
var t = new UriTemplate("/orders{?status,page}");
t.Expand(("status", "shipped"), ("page", "2"));
// Result: "/orders?status=shipped&page=2"
```

## Encoding

The library applies two encoding strategies per RFC 6570:

**Unreserved encoding** (Level 1, `.`, `/`, `;`, `?`, `&` operators): only unreserved characters (`A-Z a-z 0-9 - . _ ~`) pass through unencoded. Everything else is percent-encoded as UTF-8 bytes.

```csharp
var t = new UriTemplate("/search/{query}");
t.Expand(("query", "hello world!"));
// Result: "/search/hello%20world%21"
```

**Reserved encoding** (Level 2: `+` and `#` operators): both unreserved and reserved characters (`: / ? # [ ] @ ! $ & ' ( ) * + , ; = %`) pass through unencoded. Only characters outside both sets are percent-encoded.

```csharp
var t = new UriTemplate("{+path}");
t.Expand(("path", "/foo/bar?q=1"));
// Result: "/foo/bar?q=1"   (slashes, ?, = all preserved)
```

## Undefined Variables

When a variable referenced in the template is absent from the provided dictionary, it is treated as undefined and omitted entirely per RFC 6570 rules. No placeholder or literal `{var}` text is left in the output. Operator prefixes (`?`, `#`, `.`, `/`, `;`, `&`) are only emitted when at least one variable in the expression produces a value.

```csharp
var t = new UriTemplate("/orders{?status,page}");

// Only "status" provided; "page" is undefined:
t.Expand(("status", "shipped"));
// Result: "/orders?status=shipped"

// All variables undefined:
t.Expand(new Dictionary<string, string>());
// Result: "/orders"
```

## Level 4 Not Supported

RFC 6570 Level 4 defines two value modifiers -- prefix (`{var:3}`) and explode (`{var*}`) -- that are not supported by this library. Templates containing these modifiers throw `NotSupportedException` at construction time.

```csharp
// Throws NotSupportedException:
var t = new UriTemplate("{var:3}");
var t = new UriTemplate("{list*}");
```

Level 4 is deferred because it requires list and dictionary value types, which are beyond the current `IDictionary<string, string>` API surface. See [docs/architecture.md](docs/architecture.md) for design details.

## Additional Resources

- [Usage Guide](docs/usage.md)
- [Architecture & Design](docs/architecture.md)
- [RFC 6570 Test Plan](docs/test-plan.md)
- [RFC 6570 -- URI Template Specification](https://datatracker.ietf.org/doc/html/rfc6570)

## License

MIT -- see [LICENSE](LICENSE)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md)
