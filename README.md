# Chatter.Rest.UriTemplates

RFC 6570 URI Template expansion for .NET.

[![CI](https://github.com/brenpike/Chatter.Rest.UriTemplates/actions/workflows/uritemplate-cicd.yml/badge.svg)](https://github.com/brenpike/Chatter.Rest.UriTemplates/actions/workflows/uritemplate-cicd.yml)
[![NuGet](https://img.shields.io/nuget/v/Chatter.Rest.UriTemplates?label=Chatter.Rest.UriTemplates)](https://www.nuget.org/packages/Chatter.Rest.UriTemplates)
[![DI CI](https://github.com/brenpike/Chatter.Rest.UriTemplates/actions/workflows/uritemplate-di-cicd.yml/badge.svg)](https://github.com/brenpike/Chatter.Rest.UriTemplates/actions/workflows/uritemplate-di-cicd.yml)
[![NuGet](https://img.shields.io/nuget/v/Chatter.Rest.UriTemplates.DependencyInjection?label=Chatter.Rest.UriTemplates.DependencyInjection)](https://www.nuget.org/packages/Chatter.Rest.UriTemplates.DependencyInjection)

`Chatter.Rest.UriTemplates` helps API clients expand templated links like
`/orders/{id}{?status,page}` into safe, correctly encoded URIs. It is useful
when working with REST APIs, HAL links, OpenAPI-style client code, or any API
that returns URI templates for clients to fill in.

```csharp
using Chatter.Rest.UriTemplates;

var template = new UriTemplate("/orders/{id}{?status,page}");

var uri = template.Expand(
    ("id", "42"),
    ("status", "shipped"),
    ("page", "2"));

// "/orders/42?status=shipped&page=2"
```

## Why Use It

- Expand RFC 6570 URI Templates without hand-building paths and query strings.
- Get correct UTF-8 percent-encoding for path, query, fragment, and reserved expansions.
- Omit undefined variables according to RFC 6570 instead of leaving broken placeholders behind.
- Support all RFC 6570 Levels 1-4 operators, including prefix modifiers, explode modifiers, list values, and associative-array values.
- Small, dependency-free API.
- Target both modern .NET and broad .NET Standard consumers.

## Installation

Install from NuGet:

```bash
dotnet add package Chatter.Rest.UriTemplates
```

Then import the namespace:

```csharp
using Chatter.Rest.UriTemplates;
```

The package targets `net8.0` and `netstandard2.0` and has no external runtime
NuGet dependencies.

## Dependency Injection

A companion package provides integration with `Microsoft.Extensions.DependencyInjection`:

```bash
dotnet add package Chatter.Rest.UriTemplates.DependencyInjection
```

NuGet resolves the core `Chatter.Rest.UriTemplates` package as a transitive
dependency, so a single install command is sufficient.

Register the services during startup:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddUriTemplates();
```

Then inject `IUriTemplateFactory` to create and expand templates:

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

`AddUriTemplates()` registers `IUriTemplateParser` as a singleton and
`IUriTemplateFactory` as transient. Both use `TryAdd`, so they will not
override custom registrations. See the
[usage guide (section 11)](docs/usage.md#11-dependency-injection) for full
details.

## Quick Start

Create a template once, then expand it with either a dictionary or tuple pairs.

```csharp
var template = new UriTemplate("/search{?q,limit}");

var uri = template.Expand(new Dictionary<string, string>
{
    ["q"] = "uri templates",
    ["limit"] = "10"
});

// "/search?q=uri%20templates&limit=10"
```

Tuple pairs are convenient for short calls:

```csharp
var uri = new UriTemplate("/users/{id}")
    .Expand(("id", "brennan@example.com"));

// "/users/brennan%40example.com"
```

## Common Examples

### Path Values

```csharp
var uri = new UriTemplate("/customers/{customerId}/orders/{orderId}")
    .Expand(
        ("customerId", "cust_123"),
        ("orderId", "ord_456"));

// "/customers/cust_123/orders/ord_456"
```

### Query Strings

Use `{?var}` to start a query string and `{&var}` to append to an existing one.

```csharp
var listOrders = new UriTemplate("/orders{?status,page,pageSize}");

var uri = listOrders.Expand(
    ("status", "ready to ship"),
    ("page", "2"),
    ("pageSize", "50"));

// "/orders?status=ready%20to%20ship&page=2&pageSize=50"
```

```csharp
var uri = new UriTemplate("/orders?sort=created{&status,page}")
    .Expand(("status", "open"), ("page", "3"));

// "/orders?sort=created&status=open&page=3"
```

### Reserved Path Expansion

Use `{+var}` when a value intentionally contains URI reserved characters, such
as a path that should keep its slashes.

```csharp
var uri = new UriTemplate("/proxy/{+path}")
    .Expand(("path", "files/2026/report.pdf"));

// "/proxy/files/2026/report.pdf"
```

Using `{var}` instead would encode the slashes:

```csharp
var uri = new UriTemplate("/proxy/{path}")
    .Expand(("path", "files/2026/report.pdf"));

// "/proxy/files%2F2026%2Freport.pdf"
```

**Only expand trusted values with `{+var}` and `{#var}`.** Reserved and
fragment expansion exist to let reserved URI characters pass through
unencoded — exactly what RFC 6570 Section 3.2.3 requires — but that also means
the value can change the meaning of the surrounding URI. With
`http://ex.com/a{+p}`:

- `p = "?admin=1"` produces `http://ex.com/a?admin=1` — the value rewrites
  the query string.
- `p = "#frag"`, `p = "x@evil.com"`, and `p = "//evil.com/a"` rewrite the
  fragment, userinfo, and authority the same way.
- `p = "../../etc/passwd"` passes the traversal sequence through raw.

Under the default `{var}` operator all of these characters are
percent-encoded, so none of the above is possible. Use the default operator
for untrusted values; if reserved expansion is genuinely required, validate
the value against a caller-side allowlist first. See
[Encoding](#encoding) for how pre-encoded sequences behave under `{+}` and
`{#}`.

### Optional Variables

Variables that are not supplied are omitted. Prefixes such as `?`, `&`, `/`,
and `.` are only emitted when at least one variable in the expression has a
value.

```csharp
var template = new UriTemplate("/orders/{id}{?status,page}");

var uri = template.Expand(("id", "42"), ("status", "open"));

// "/orders/42?status=open"
```

If every query variable is missing, no query string is added:

```csharp
var uri = new UriTemplate("/orders{?status,page}")
    .Expand(new Dictionary<string, string>());

// "/orders"
```

### Empty Values

Empty strings are defined values and are expanded according to the operator.

```csharp
new UriTemplate("/orders{?status}")
    .Expand(("status", ""));
// "/orders?status="

new UriTemplate("/matrix{;flag}")
    .Expand(("flag", ""));
// "/matrix;flag"
```

### Inspect Required Variables

Use `GetVariables()` when you need to validate input, build UI prompts, or log
which values a template expects.

```csharp
var template = new UriTemplate("/orders/{id}{?status,page}{&locale}");

var variables = template.GetVariables();

// ["id", "status", "page", "locale"]
```

## API

### `new UriTemplate(string template)`

Parses the template eagerly.

- Throws `ArgumentNullException` when `template` is `null`.
- Throws `FormatException` for malformed templates, such as unclosed braces,
  nested braces, empty `{}` expressions, or mutually exclusive prefix and
  explode modifiers.

For dependency injection scenarios, use `IUriTemplateFactory.Create(string)`
instead of calling the constructor directly. See the
[Dependency Injection](#dependency-injection) section.

### `Expand(IDictionary<string, string> variables)`

Expands the template using string values from a dictionary.

```csharp
var uri = new UriTemplate("/search{?q,lang}")
    .Expand(new Dictionary<string, string>
    {
        ["q"] = "dotnet",
        ["lang"] = "en"
    });

// "/search?q=dotnet&lang=en"
```

### `Expand(IDictionary<string, object?> variables)`

Expands the template using a dictionary that supports composite value types for
Level 4 expansion. Supported value types: `string`, `IEnumerable<string>`,
`IDictionary<string, string>`, `IEnumerable<KeyValuePair<string, string>>`, and
`null` (treated as undefined).

```csharp
var uri = new UriTemplate("{?list*}").Expand(new Dictionary<string, object?>
{
    ["list"] = new[] { "a", "b", "c" }
});

// "?list=a&list=b&list=c"
```

### `Expand(IDictionary<string, UriTemplateValue> variables)`

Strongly-typed alternative to the `object?` overload. Values are created through
static factory methods on `UriTemplateValue`:

```csharp
var uri = new UriTemplate("{?color*}").Expand(new Dictionary<string, UriTemplateValue>
{
    ["color"] = UriTemplateValue.From(new[] { "red", "green", "blue" })
});

// "?color=red&color=green&color=blue"
```

Factory methods: `From(string)` returning `StringValue`,
`From(IEnumerable<string>)` returning `ListValue`,
`From(IDictionary<string, string>)` returning `DictionaryValue`.

### `Expand(params (string Key, string Value)[] variables)`

Expands the template using tuple pairs. If the same key is supplied more than
once, the first value wins.

```csharp
var uri = new UriTemplate("/search{?q}")
    .Expand(("q", "first"), ("q", "second"));

// "/search?q=first"
```

### `Expand(params (string Key, object? Value)[] variables)`

Tuple overload for composite values. Accepts string, list, dictionary, and null
values via `object?`. First-wins for duplicate keys.

```csharp
var uri = new UriTemplate("/users/{id}{?tag*}").Expand(
    ("id", (object?)"42"),
    ("tag", (object?)new[] { "active", "premium" })
);

// "/users/42?tag=active&tag=premium"
```

> Passing a `UriTemplateValue` instance through this overload throws
> `FormatException`. Use `Expand(IDictionary<string, UriTemplateValue>)` for
> strongly-typed values.

### `Expand()`

Expands the template with all variables undefined. Every expression is omitted
per RFC 6570 rules.

```csharp
var uri = new UriTemplate("/orders{?status,page}").Expand();

// "/orders"
```

### `GetVariables()`

Returns variable names in first-seen order with duplicates removed. Variable
names are case-sensitive.

```csharp
var variables = new UriTemplate("/{resource}/{id}{?id,format}")
    .GetVariables();

// ["resource", "id", "format"]
```

RFC 6570 permits duplicate variable names, and each occurrence expands:
`{?x,x}` with `x = "1"` produces `?x=1&x=1`, while `GetVariables()` reports
`x` once. Callers that count expansion output via `GetVariables()` will
under-count in that case.

## Supported Template Features

The library supports RFC 6570 Levels 1-4.

| Operator | Level | Purpose | Example |
| --- | --- | --- | --- |
| `{var}` | 1 | Simple string expansion | `/users/{id}` |
| `{+var}` | 2 | Reserved expansion | `/proxy/{+path}` |
| `{#var}` | 2 | Fragment expansion | `/docs{#section}` |
| `{.var}` | 3 | Label expansion | `/api{.version}` |
| `{/var}` | 3 | Path segment expansion | `/files{/folder,name}` |
| `{;var}` | 3 | Path-style parameters | `/matrix{;x,y}` |
| `{?var}` | 3 | Form-style query | `/orders{?status,page}` |
| `{&var}` | 3 | Form-style query continuation | `/orders?sort=date{&page}` |
| `{var:3}` | 4 | Prefix modifier | `/search{?q:10}` |
| `{var*}` | 4 | Explode modifier | `/items{?tag*}` |

Variables are supplied as `string` for simple values. Level 4 composite values
(lists and associative arrays) are supported via the
`Expand(IDictionary<string, object?>)` overload or the strongly-typed
`Expand(IDictionary<string, UriTemplateValue>)` overload.

Note: a template that begins with `{/...}` can produce a scheme-relative URL
when the first variable expands to an empty string — `{/a,b}` with `a = ""`
and `b = "evil.com"` produces `//evil.com`. This is RFC-conformant, but if a
template starts with `{/...}` and its values are not trusted, prefix the
template with a literal path segment.

## Encoding

`Chatter.Rest.UriTemplates` percent-encodes values as UTF-8 bytes.

- Simple, label, path segment, path-style parameter, and query operators encode
  everything except RFC 3986 unreserved characters: `A-Z a-z 0-9 - . _ ~`.
- Reserved and fragment operators preserve reserved URI characters such as `/`,
  `?`, `#`, `&`, and `=`.
- Already percent-encoded sequences are preserved for reserved and fragment
  expansion.

```csharp
new UriTemplate("/search/{q}")
    .Expand(("q", "hello world!"));
// "/search/hello%20world%21"

new UriTemplate("{+url}")
    .Expand(("url", "https://example.com/docs?q=uri%20templates"));
// "https://example.com/docs?q=uri%20templates"
```

Preserving pre-encoded sequences is part of the `{+}`/`{#}` trust boundary: a
valid-looking percent triplet in the value is kept verbatim, so `{+p}` with
`p = "%0d%0aX: y"` yields `%0d%0aX:%20y` and `p = "%2e%2e%2fetc"` stays
`%2e%2e%2fetc` — a downstream server that decodes these sees control
characters or a traversal sequence. The default operator neutralizes the same
input by encoding `%` as `%25`. This difference between the two operator
families is the reason `{+}` and `{#}` values must be trusted; expand
`{+url}`-style templates only with URLs you already trust.

Even under `{+}` and `{#}`, characters outside the reserved and unreserved
sets are always percent-encoded: raw CR, LF, NUL, backslash, and
direction-override characters such as U+202E never pass through (`"a\r\nb"`
becomes `a%0D%0Ab`), so expansion cannot split HTTP headers, and non-ASCII
text is always UTF-8 percent-encoded, so raw homograph bytes never reach a
host position.

## Level 4: Prefix, Explode, and Composite Values

Level 4 templates use the `Expand(IDictionary<string, object?>)` overload to
supply list and associative-array values alongside strings.

```csharp
// Prefix modifier: truncate value to 3 characters
var uri = new UriTemplate("{var:3}").Expand(new Dictionary<string, object?>
{
    ["var"] = "value"
});
// "val"

// List value with explode: expand each member as a query parameter
var uri = new UriTemplate("{?color*}").Expand(new Dictionary<string, object?>
{
    ["color"] = new[] { "red", "green", "blue" }
});
// "?color=red&color=green&color=blue"

// Associative array with explode
var keys = new List<KeyValuePair<string, string>>
{
    new("semi", ";"),
    new("dot", "."),
};

var uri = new UriTemplate("{?keys*}").Expand(new Dictionary<string, object?>
{
    ["keys"] = keys
});
// "?semi=%3B&dot=."
```

The `Expand(IDictionary<string, string>)` overload continues to work for
string-only values, including templates with prefix modifiers.

## More Documentation

- [Usage guide](docs/usage.md)
- [Architecture and design](docs/architecture.md)
- [Development guide](docs/development.md)
- [RFC 6570 test plan](docs/test-plan.md)
- [Backlog](docs/backlog.md)
- [RFC 6570 specification](https://datatracker.ietf.org/doc/html/rfc6570)

## Development

Build and test locally with the .NET SDK:

```bash
dotnet restore
dotnet test
```

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. See [LICENSE](LICENSE).
