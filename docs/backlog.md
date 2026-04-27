# Backlog

This file captures deferred work and follow-ups that are not blocking current releases. Items here represent API improvements, ergonomic enhancements, or structural changes identified during development that were intentionally deferred to keep the current release focused.

---

## `UriTemplateValue` union type

**Summary:** Replace the `object?` value parameter in `Expand(IDictionary<string, object?>)` with a strongly-typed value union. The new `UriTemplateValue` type would provide static factory methods:

- `UriTemplateValue.FromString(string)`
- `UriTemplateValue.FromList(IEnumerable<string>)`
- `UriTemplateValue.FromDictionary(IDictionary<string, string>)`

**Rationale:** The current `IDictionary<string, object?>` overload relies on runtime type dispatch and provides no compile-time safety for callers. A dedicated value type improves API discoverability, enables better IntelliSense guidance, and catches invalid value types at compile time rather than throwing `FormatException` at expansion time.

**Deferred because:** The `IDictionary<string, object?>` overload is sufficient for the initial Level 4 release and follows a common .NET pattern for mixed-type dictionaries. The union type can be added as a non-breaking addition alongside the existing overload.

**Acceptance criteria:**
- New public `UriTemplateValue` type added with the three static factory methods.
- New `Expand(IDictionary<string, UriTemplateValue>)` overload on `UriTemplate`.
- Runtime dispatch in `UriTemplateExpander` adapted to accept `UriTemplateValue`.
- Existing `Expand(IDictionary<string, object?>)` overload preserved for backward compatibility.
- Tests cover all three value kinds through the new overload.
- Documentation updated (`docs/usage.md`, `docs/architecture.md`, `README.md`).

---

## Tuple overload for composite values

**Summary:** Add a `params (string Key, object? Value)[]` overload on `UriTemplate` so callers who currently use `params (string, string)[]` can pass list and dictionary values without manually building an `IDictionary<string, object?>`.

**Rationale:** The existing string-tuple overload (`params (string Key, string Value)[]`) is the most ergonomic API for simple cases. Callers who want to mix string and composite values currently must switch to the dictionary overload, losing the tuple convenience. A composite-tuple overload restores ergonomic parity.

**Deferred because:** The dictionary overload provides full Level 4 functionality. The tuple overload is a convenience improvement and can be added without breaking changes.

**Acceptance criteria:**
- New `Expand(params (string Key, object? Value)[] variables)` overload on `UriTemplate`.
- Delegates to the canonical `ExpandCore` path via `IDictionary<string, object?>`.
- First-wins duplicate handling consistent with the existing string-tuple overload.
- Tests cover string, list, and dictionary value passing through the new overload.
- Documentation updated (`docs/usage.md`, `docs/architecture.md`, `README.md`).
