# Chatter.Rest.UriTemplates

A standalone .NET library that expands RFC 6570 URI Templates (Levels 1-4) into correctly percent-encoded URIs for REST, HAL, and OpenAPI-style API clients.

## Language

### Template structure

**URI Template**: A string containing literal text and `{...}` expressions that expands into a URI once variable values are supplied, per RFC 6570.

**Token**: One parsed unit of a URI Template — either a literal or an expression. _Avoid_: node, segment, part.

**Literal**: A run of template text outside any expression, validated and where
necessary UTF-8 percent-encoded during parsing, after which the resulting token
value is emitted unchanged during expansion. _Avoid_: static text, constant.

**Expression**: The `{...}` construct inside a template, consisting of an optional operator followed by one or more variable specifiers. _Avoid_: placeholder, slot, binding.

**Variable Specifier**: A single variable reference inside an expression, carrying its name and any Level 4 modifiers. _Avoid_: var spec, parameter, argument.

**Operator**: The leading sigil of an expression (none, `+`, `#`, `.`, `/`, `;`, `?`, `&`) that selects the expansion semantics and encoding rules for that expression.

**Operator Strategy**: The implementation that owns one operator's prefix, separator, name-emission, and encoding behavior. _Avoid_: handler, rule, policy.

### Modifiers

**Prefix Modifier**: The `:N` suffix on a variable specifier that truncates a string value to its first N characters. _Avoid_: substring modifier, truncation modifier, max-length.

**Explode Modifier**: The `*` suffix on a variable specifier that expands each member of a list or associative array separately instead of joining them with commas. _Avoid_: star modifier, splat, spread.

### Values

**Template Value**: A supplied variable value in one of three shapes — string, list, or associative array — that determines how a variable specifier expands.

**Associative Array**: A name/value mapping supplied as a variable value, expanded as `key,value` pairs or as `key=value` pairs when exploded. _Avoid_: map, dictionary value, hash, object.

**List Value**: An ordered collection supplied as a variable value, joined by commas or exploded per the operator. _Avoid_: array, sequence, collection.

**Undefined Variable**: A variable named in an expression that has no supplied value (absent key, null, or empty list/associative array), and which RFC 6570 requires be omitted from the output rather than rendered as an empty placeholder. _Avoid_: missing variable, unset, null variable.

### Expansion

**Expansion**: The act of turning a template plus variable values into a URI string. _Avoid_: rendering, interpolation, formatting, substitution.

**Expander**: The component that expands one **Expression** into its output string. It does not walk the template — `UriTemplate` owns token traversal and literal appending. _Avoid_: renderer, formatter, engine.

**Simple Expansion**: Level 1 expansion under the no-operator form `{var}`, percent-encoding everything outside the unreserved set.

**Reserved Expansion**: Level 2 expansion under `{+var}`, which lets reserved characters and existing percent-triplets pass through unencoded.

**Fragment Expansion**: Level 2 expansion under `{#var}`, which prefixes `#` and otherwise follows reserved-expansion encoding.

**Label Expansion**: Level 3 expansion under `{.var}`, which prefixes and separates values with `.`.

**Path Segment Expansion**: Level 3 expansion under `{/var}`, which prefixes and separates values with `/`.

**Path-Style Parameter Expansion**: Level 3 expansion under `{;var}`, which emits `;name=value` pairs and drops the `=` for empty values.

**Query Expansion**: Level 3 expansion under `{?var}`, which opens a query string with `?` and emits `name=value` pairs separated by `&`.

**Query Continuation Expansion**: Level 3 expansion under `{&var}`, which appends further `name=value` pairs to an existing query string starting with `&`.

**Level**: The RFC 6570 conformance tier (1 through 4) that a template feature belongs to, used to scope both the operator set and the test suites.

### Encoding

**Unreserved Set**: The RFC 3986 characters (`A-Z a-z 0-9 - . _ ~`) that are never percent-encoded during expansion.

**Reserved Set**: The RFC 3986 gen-delims and sub-delims that reserved and fragment expansion pass through unencoded but simple expansion encodes.

**Percent-Encoding**: The UTF-8-based `%XX` transformation applied to value characters that the active operator does not permit literally. _Avoid_: URL encoding, escaping, quoting.

**Compliance Suite**: The official RFC 6570 example, extended, and negative test data consumed by the compliance tests to prove spec conformance. _Avoid_: golden tests, conformance data, spec fixtures.

## Relationships

- A **URI Template** is parsed into an ordered list of **Tokens**; each Token is either a **Literal** or an **Expression**.
- An **Expression** has exactly one **Operator** and one or more **Variable Specifiers**.
- A **Variable Specifier** carries at most one modifier: a **Prefix Modifier** or an **Explode Modifier**, never both — the parser rejects the combination.
- An **Operator** resolves to exactly one **Operator Strategy**, which owns that operator's separators and its **Unreserved Set** / **Reserved Set** encoding choice.
- A **Prefix Modifier** applies only to string **Template Values**; applying it to a **List Value** or **Associative Array** is an error.
- An **Explode Modifier** changes output only for **List Values** and **Associative Arrays**.
- An **Undefined Variable** produces no output and no separator, so surrounding **Literals** join directly.
- **URI Template** walks its **Tokens**, appending each **Literal**'s value directly and delegating each **Expression** to the **Expander**, which expands that one expression and delegates per-value **Percent-Encoding** to the active **Operator Strategy**.
- Each **Level** subsumes the levels below it: Level 4 templates may use every Operator plus both modifiers.
- The **Compliance Suite** exercises **Expansion** across all Levels and Operators.

## Example dialogue

> **Dev:** "If `status` is an empty list and the expression is `{?status,page}`, do I get `?&page=2`?"
> **Domain expert:** "No. An empty list is an **Undefined Variable**, so it contributes nothing — not even a separator. **Query Expansion** emits `?` before the first *defined* variable only, so you get `?page=2`. An empty *string* is different: that one is defined, and you would get `?status=&page=2`."

## Flagged ambiguities

- "Variable" is used in the artifacts for both the **Variable Specifier** (the template-side reference, with its modifiers) and the supplied **Template Value** (the caller-side data). Prefer the specific term when the distinction matters.
- "Encode" appears both for **Percent-Encoding** a single value and for the whole **Expansion** of a template. Reserve "encode" for the character-level transformation.
