# Backlog

This file captures deferred work and follow-ups that are not blocking current releases. Items here represent API improvements, ergonomic enhancements, or structural changes identified during development that were intentionally deferred to keep the current release focused.

---

## ~~`UriTemplateValue` union type~~ (complete)

Implemented. See `UriTemplateValue` in `src/Chatter.Rest.UriTemplates/UriTemplateValue.cs` and the `Expand(IDictionary<string, UriTemplateValue>)` overload on `UriTemplate`.

---

## ~~Tuple overload for composite values~~ (complete)

Implemented. Both `Expand(params (string Key, object? Value)[])` and `Expand(params (string Key, UriTemplateValue Value)[])` overloads were added to `UriTemplate`. Each delegates to its dictionary counterpart with first-wins duplicate handling.
