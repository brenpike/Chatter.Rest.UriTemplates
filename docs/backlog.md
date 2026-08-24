# Backlog

This file captures deferred work and follow-ups that are not blocking current releases. Items here represent API improvements, ergonomic enhancements, or structural changes identified during development that were intentionally deferred to keep the current release focused.

---

## ~~`UriTemplateValue` union type~~ (complete)

Implemented. See `UriTemplateValue` in `src/Chatter.Rest.UriTemplates/UriTemplateValue.cs` and the `Expand(IDictionary<string, UriTemplateValue>)` overload on `UriTemplate`.

---

## ~~Tuple overload for composite values~~ (complete)

Implemented. `Expand(params (string Key, object? Value)[])` was added to `UriTemplate`. It delegates to its dictionary counterpart with first-wins duplicate handling. A `UriTemplateValue` tuple overload was originally added as well but was removed due to overload resolution ambiguity; callers who need typed values use `Expand(IDictionary<string, UriTemplateValue>)` instead.

---

## Contract test: no value content in exception messages

Deferred from the PR #68 documentation remediation (Codex thread: https://github.com/brenpike/Chatter.Rest.UriTemplates/pull/68#discussion_r3840122958). The "Exception message content" section of `docs/usage.md` commits the library to never interpolating supplied value content (string values, list elements, associative-array values) into an exception message. Add a contract test enforcing that commitment across all `throw` sites under `src/Chatter.Rest.UriTemplates/`.

Deferred because that remediation run was documentation-only by constraint; a test is a code change. Bounded impact: the commitment was verified manually against every existing throw site during the remediation, and a regression would require a future code change, which is itself review-gated, so risk until the test exists is low.

Note for the test author: the commitment covers value content only. Keys and runtime type names may legitimately appear in messages (the null-value message quotes the offending key; the unsupported-type message quotes the value's runtime type name), so a naive "no supplied data in messages" assertion would fail against correct code.
