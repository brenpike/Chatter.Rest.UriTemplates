# Backlog

This file captures deferred work and follow-ups that are not blocking current releases. Items here represent API improvements, ergonomic enhancements, or structural changes identified during development that were intentionally deferred to keep the current release focused.

---

## ~~`UriTemplateValue` union type~~ (complete)

Implemented. See `UriTemplateValue` in `src/Chatter.Rest.UriTemplates/UriTemplateValue.cs` and the `Expand(IDictionary<string, UriTemplateValue>)` overload on `UriTemplate`.

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
