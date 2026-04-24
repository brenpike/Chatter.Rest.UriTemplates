# CLAUDE.md

## Project Overview

Chatter.Rest.UriTemplates is a standalone .NET/C# library implementing RFC 6570 URI Template expansion for Levels 1-3. It provides simple string, reserved, fragment, label, path segment, path-style parameter, form-style query, and query continuation expansion with no external NuGet dependencies.

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
| [docs/branching-pr-workflow.md](docs/branching-pr-workflow.md) | Mandatory branching and PR workflow |

## Solution Structure

| Project | NuGet Package |
|---|---|
| `src/Chatter.Rest.UriTemplates/` | `Chatter.Rest.UriTemplates` |
| `test/Chatter.Rest.UriTemplates.Tests/` | - |

## Multi-Agent Governance

This repository uses a constrained multi-agent workflow.

Canonical governance files:
- `agent-system-policy.md` - shared agent roles, authority, tool policy, escalation, and reporting
- `docs/branching-pr-workflow.md` - MANDATORY branching, commit, PR, merge, and validation workflow

These files must ALWAYS be respected unless the user says otherwise.

Role-specific behavior is defined in:
- `orchestrator.md`
- `planner.md`
- `coder.md`
- `designer.md`

## Build and Test Commands

See [docs/development.md](docs/development.md) for all build, test, and pack commands.

## Architecture

See [docs/architecture.md](docs/architecture.md) for engine design, operator reference, encoding rules, and type design.

## Testing Conventions

See [docs/development.md](docs/development.md) for test framework, assertions, naming conventions, and coverage setup.

## Code Style and Conventions

See [docs/development.md](docs/development.md) for editorconfig rules (line endings, indentation, braces).

Additional conventions observed in the codebase:

- `UriTemplate` is the only public type; all other types (`UriTemplateParser`, `UriTemplateExpander`, `UriTemplateExpression`, `UriTemplateOperator`) are internal
- All types use the `Chatter.Rest.UriTemplates` namespace
- Internal members are exposed to test assembly via `InternalsVisibleTo` in the csproj

## Test Plan

`docs/test-plan.md` maps every RFC 6570 normative and behavioral requirement to testable scenarios, organized by level and operator. Consult it when:
- Answering questions about expected behavior
- Adding new tests for spec compliance
- Evaluating whether a bug is a spec violation or implementation choice

## CI/CD

See [docs/development.md](docs/development.md) for CI/CD workflow details.

## Package Versions

- `Chatter.Rest.UriTemplates` - v0.1.0

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
