# CLAUDE.md

## Project Overview

Chatter.Rest.UriTemplates is a standalone .NET/C# library implementing RFC 6570 URI Template expansion for Levels 1-4. It provides simple string, reserved, fragment, label, path segment, path-style parameter, form-style query, and query continuation expansion with prefix modifier, explode modifier, list value, and associative-array value support. The core package has no external NuGet dependencies. A companion DI extension package (`Chatter.Rest.UriTemplates.DependencyInjection`) depends on `Microsoft.Extensions.DependencyInjection.Abstractions 8.0.0`.

**Repository:** https://github.com/brenpike/Chatter.Rest.UriTemplates
**RFC 6570 Specification:** https://datatracker.ietf.org/doc/html/rfc6570
**License:** MIT
**Author:** Brennan Pike

## Documentation Index

| Doc | Contents |
|---|---|
| [docs/architecture.md](docs/architecture.md) | URI template engine design, operator reference, encoding rules, type design |
| [docs/usage.md](docs/usage.md) | Practical usage guide with API reference and operator examples |
| [docs/test-plan.md](docs/test-plan.md) | RFC 6570 test coverage map, test scenarios by level and operator |
| [docs/development.md](docs/development.md) | Build commands, test commands, NuGet packaging, CI/CD parity, code style, test conventions |
| [docs/backlog.md](docs/backlog.md) | Deferred work and follow-ups |
| [AGENTS.md](AGENTS.md) | External AI reviewer (Codex) guidance |

## Solution Structure

| Project | NuGet Package |
|---|---|
| `src/Chatter.Rest.UriTemplates/` | `Chatter.Rest.UriTemplates` |
| `src/Chatter.Rest.UriTemplates.DependencyInjection/` | `Chatter.Rest.UriTemplates.DependencyInjection` |
| `test/Chatter.Rest.UriTemplates.Tests/` | - |

## Multi-Agent Governance

This repository uses a constrained multi-agent workflow powered by the `agent-framework@brenpike` plugin (configured in `.claude/settings.json`).

Governance, agent roles, branching/commit/PR workflow, versioning policy, and PR review remediation are defined in the plugin. Canonical governance files are installed under `<claude-plugins-cache>/brenpike/agent-framework/<version>/governance/` by the Claude Code plugin system.

External AI reviewer (Codex) guidance: `AGENTS.md`

## Build and Test Commands

See [docs/development.md](docs/development.md) for all build, test, and pack commands.

## Architecture

See [docs/architecture.md](docs/architecture.md) for engine design, operator reference, encoding rules, and type design.

## Testing Conventions

See [docs/development.md](docs/development.md) for test framework, assertions, naming conventions, and coverage setup.

## Code Style and Conventions

See [docs/development.md](docs/development.md) for editorconfig rules (line endings, indentation, braces).

Additional conventions observed in the codebase:

- Public API (`Chatter.Rest.UriTemplates` package): `UriTemplate`, `IUriTemplateParser`, `IUriTemplateFactory`, `UriTemplateToken` (abstract base), `UriTemplateLiteralToken`, `UriTemplateExpressionToken`, `UriTemplateOperator`, `UriTemplateVarSpec`; public API (`Chatter.Rest.UriTemplates.DependencyInjection` package): `ServiceCollectionExtensions` (`AddUriTemplates` extension method); internal implementation types (not public): `UriTemplateParser`, `UriTemplateExpander`, `UriTemplateFactory`; `UriTemplateExpression` was removed (replaced by `UriTemplateExpressionToken`)
- All types use the `Chatter.Rest.UriTemplates` namespace
- Internal members are exposed to test assembly via `InternalsVisibleTo` in the csproj

## Test Plan

`docs/test-plan.md` maps every RFC 6570 normative and behavioral requirement to testable scenarios, organized by level and operator. Consult it when:
- Answering questions about expected behavior
- Adding new tests for spec compliance
- Evaluating whether a bug is a spec violation or implementation choice

## CI/CD

See [docs/development.md](docs/development.md) for CI/CD workflow details.

## Versioning

### Artifact

| Artifact | NuGet Package ID | Current Version |
|---|---|---|
| `Chatter.Rest.UriTemplates` | `Chatter.Rest.UriTemplates` | `0.5.0` |
| `Chatter.Rest.UriTemplates.DependencyInjection` | `Chatter.Rest.UriTemplates.DependencyInjection` | `0.1.1` |

### Canonical Version Source

`src/Chatter.Rest.UriTemplates/Chatter.Rest.UriTemplates.csproj` — `<Version>` element is the single source of truth for the core package. `src/Chatter.Rest.UriTemplates.DependencyInjection/Chatter.Rest.UriTemplates.DependencyInjection.csproj` — `<Version>` element is the single source of truth for the DI extension package (versioned independently).

### Files to Update Atomically on Version Bump

All of the following must be updated together in the same commit when bumping `Chatter.Rest.UriTemplates`:

1. `src/Chatter.Rest.UriTemplates/Chatter.Rest.UriTemplates.csproj` — `<Version>` element (canonical)
2. `CLAUDE.md` — version in this table
3. `docs/development.md` — line that reads `Package ID: \`Chatter.Rest.UriTemplates\` vX.Y.Z`

All of the following must be updated together in the same commit when bumping `Chatter.Rest.UriTemplates.DependencyInjection`:

1. `src/Chatter.Rest.UriTemplates.DependencyInjection/Chatter.Rest.UriTemplates.DependencyInjection.csproj` — `<Version>` element (canonical)
2. `CLAUDE.md` — version in the Artifact table
3. `docs/development.md` — line that reads `Package ID: \`Chatter.Rest.UriTemplates.DependencyInjection\` vX.Y.Z`

### Bump-Triggering Paths

A version bump is required when a PR modifies any file under:
- `src/Chatter.Rest.UriTemplates/**`
- `src/Chatter.Rest.UriTemplates.DependencyInjection/**`

No bump required for changes to: `test/**`, `docs/**`, `.github/**`, `*.md` (root), agent framework files.

### Changelog

No `CHANGELOG.md` exists. Release notes are not currently maintained. A changelog file may be added in a future chore.

### Git Tags

Tags are created by CI via `create-version-tag.yml`, called by each caller workflow after a successful deploy on merge to `main`. Tag format: `uritemplate/vX.Y.Z` (core package) and `uritemplate-di/vX.Y.Z` (DI extension package).

### NuGet Publish

CI publishes to NuGet.org automatically on merge to `main`. The core package is published via `uritemplate-cicd.yml` and the DI extension package via `uritemplate-di-cicd.yml`. Both workflows use the `NUGET_API_KEY_CHATTER_URITEMPLATE` secret.

## Memory Usage

- Use `claude-mem` first when prior context, earlier decisions, constraints, risks, or continuity may materially improve accuracy, efficiency, or consistency.
- Treat memory as a continuity and token-efficiency aid, not as a substitute for current repo inspection, validation, or other required verification.
- Reuse still-valid prior context when helpful, but continue normally if no relevant memory is found.
- If `mem-search` or another memory tool fails, retry at most once if the failure appears transient, then fall back to normal tools and available context.
- Memory-tool failure alone must not block execution.

## Codebase Exploration Guidance

Use local repo inspection first for codebase exploration and change understanding.

Preferred tools:
- `Read` for targeted file inspection
- `Grep` and `Glob` for discovery
- read-only shell commands for repository structure and search
- `Context7` only when external framework, library, platform, or API documentation is needed
- `claude-mem` when prior project or session context can reduce rediscovery

For code review, debugging, and refactoring:
1. start with the smallest local inspection that can answer the question
2. widen scope only when necessary
3. validate conclusions with the actual files being changed
