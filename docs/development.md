# Development Guide

## 1. Prerequisites

- **.NET 8.0 SDK (8.0.x)** — pinned in `global.json` (version `8.0.100`, `rollForward: latestFeature` — any 8.0.x SDK at or above 8.0.100 satisfies the pin)
- **Language:** C# 10.0 (set per project via `<LangVersion>10.0</LangVersion>`)
- No external tools required beyond the .NET SDK

## 2. Solution Structure

```
Chatter.Rest.UriTemplates.sln             <- solution file (repo root)
src/
  Directory.Build.props                   <- shared build/analyzer/audit settings for src/ projects
  Chatter.Rest.UriTemplates/
    Chatter.Rest.UriTemplates.csproj      <- targets net8.0;netstandard2.0
    UriTemplate.cs                        <- public entry point
    ...                                   <- parser, expander, encoder, value types,
                                             operator strategies (Operators/), and more
  Chatter.Rest.UriTemplates.DependencyInjection/
    Chatter.Rest.UriTemplates.DependencyInjection.csproj  <- targets net8.0;netstandard2.0
    ServiceCollectionExtensions.cs        <- AddUriTemplates() extension method
test/
  Chatter.Rest.UriTemplates.Tests/
    Chatter.Rest.UriTemplates.Tests.csproj  <- targets net8.0
    *Tests.cs                               <- 15 test classes; complete list in section 4
    uritemplate-test/                       <- official RFC 6570 test-suite submodule (JSON fixtures)
  Chatter.Rest.UriTemplates.DependencyInjection.Tests/
    Chatter.Rest.UriTemplates.DependencyInjection.Tests.csproj  <- targets net8.0
    ServiceCollectionExtensionsTests.cs
```

The source-file listing above is deliberately **non-exhaustive** — it names entry
points only. See the project directories for the full file list.

**Target frameworks:**

- both `src/` projects multi-target `net8.0;netstandard2.0`
- both `test/` projects target `net8.0` only

## 3. Build Commands

```bash
# Initialize the uritemplate-test submodule (required for the compliance tests)
git submodule update --init

# Restore (locked mode - requires packages.lock.json)
dotnet restore --locked-mode

# Build (Debug)
dotnet build

# Build (Release, skip restore)
dotnet build -c Release --no-restore
```

**Lock file note:** each of the four projects (both `src/` projects and both `test/` projects) has its own `packages.lock.json` in its project directory. If `dotnet restore --locked-mode` fails, delete the failing project's `packages.lock.json` and run `dotnet restore` to regenerate it. Commit the regenerated file(s).

**Warnings:** `src/Directory.Build.props` sets `TreatWarningsAsErrors` with `AnalysisLevel` `latest-recommended` for the `src/` projects, so most warnings fail the build. Three analyzer rules with pre-existing findings are excluded from errors via `WarningsNotAsErrors` and still report as warnings on **both** target frameworks: `CA1305`, `CA1510`, `CA1716` (27 warnings in a full Release build as of this writing — see the follow-up notes in `src/Directory.Build.props`). `NU1900` is permanently exempted so an audit-service outage cannot fail restore. No other warnings are expected; in particular there are no nullable-context warnings on either target framework.

## 4. Test Commands

`UriTemplateComplianceTests` reads the official RFC 6570 suite from the
`uritemplate-test` submodule. Run `git submodule update --init` first, or those
tests throw `DirectoryNotFoundException` instead of skipping — on a clean tree
the `TestData/` directory is never created at all.

```bash
# Run all tests
dotnet test

# Run URI template tests only
dotnet test test/Chatter.Rest.UriTemplates.Tests/Chatter.Rest.UriTemplates.Tests.csproj

# Run with filter (e.g., Level 1 only)
dotnet test --filter FullyQualifiedName~Level1
```

CI runs tests with `-c Release --no-build` after a Release build step. To replicate exactly:

```bash
# Core package CI/CD test parity (uritemplate-cicd.yml)
dotnet build -c Release --no-restore
dotnet test test/Chatter.Rest.UriTemplates.Tests/Chatter.Rest.UriTemplates.Tests.csproj -c Release --no-build
```

```bash
# DI package CI/CD test parity (uritemplate-di-cicd.yml)
dotnet build -c Release --no-restore
dotnet test test/Chatter.Rest.UriTemplates.DependencyInjection.Tests/Chatter.Rest.UriTemplates.DependencyInjection.Tests.csproj -c Release --no-build
```

**Test classes (complete list — update this section when adding or removing a test class):**

Core test project (`test/Chatter.Rest.UriTemplates.Tests/`), 15 classes:

- `UriTemplateLevel1Tests` — simple string expansion
- `UriTemplateLevel2Tests` — reserved and fragment expansion
- `UriTemplateLevel3Tests` — multi-variable and operator expansion
- `UriTemplateLevel4Tests` — prefix modifiers, list values, associative arrays, explode behavior
- `UriTemplateArgumentContractTests` — argument contracts of the public `Expand` overloads (null handling, at-most-once enumeration, failure ordering)
- `UriTemplateAssociativeArrayTests` — associative-array expansion determinism guarantees
- `UriTemplateComplianceTests` — official RFC 6570 suite, read from the `TestData/` JSON fixtures (see section 8)
- `UriTemplateEdgeCaseTests` — malformed templates, mixed-level, modifier validation
- `UriTemplateGetVariablesTests` — variable enumeration across all operator types
- `UriTemplateParserValidationTests` — parser/encoder rejection of invalid input
- `UriTemplateSecurityTests` — characterization of the expansion security posture
- `UriTemplateTupleOverloadTests` — tuple-based `Expand` overloads
- `UriTemplateTypeValidationTests` — construction-time validation of token types and the operator strategy factory
- `UriTemplateValueTests` — `UriTemplateValue` factory methods and the `UriTemplateValue`-dictionary `Expand` overload
- `XmlDocExceptionTagShapeTests` — mechanical enforcement of the `<exception>`-tag shape convention against the core assembly's generated XML documentation (see section 7)

DI test project (`test/Chatter.Rest.UriTemplates.DependencyInjection.Tests/`), 1 class:

- `ServiceCollectionExtensionsTests` — `AddUriTemplates()` service registration

## 5. NuGet Packaging

```bash
dotnet pack src/Chatter.Rest.UriTemplates/Chatter.Rest.UriTemplates.csproj -c Release -o publish/nuget
```

Output lands in `publish/nuget/`. Package ID: `Chatter.Rest.UriTemplates` v0.11.0.

To pack the DI extension package:

```bash
dotnet pack src/Chatter.Rest.UriTemplates.DependencyInjection/Chatter.Rest.UriTemplates.DependencyInjection.csproj -c Release -o publish/nuget
```

Package ID: `Chatter.Rest.UriTemplates.DependencyInjection` v0.3.0.

## 6. CI/CD Parity

Two per-project workflows in `.github/workflows/`. Each workflow covers one NuGet package independently.

### Core package — `uritemplate-cicd.yml`

| Trigger | Condition |
|---|---|
| Push | `feature/**`, `bugfix/**`, `hotfix/**`, `refactor/**`, `chore/**`, `docs/**`, `test/**`, `ci/**`, `main` (path-scoped to `src/Chatter.Rest.UriTemplates/`, `src/Directory.Build.props`, `test/Chatter.Rest.UriTemplates.Tests/`, plus its own workflow file and the two reusable workflows `version-check.yml` and `create-version-tag.yml`) |
| Pull request | targeting `main` (same path scope) |
| Manual | `workflow_dispatch` — runs the build job only; deploy and tag never run for manual dispatches |

### DI package — `uritemplate-di-cicd.yml`

| Trigger | Condition |
|---|---|
| Push | same branch patterns (path-scoped to `src/Chatter.Rest.UriTemplates.DependencyInjection/`, `test/Chatter.Rest.UriTemplates.DependencyInjection.Tests/`, `src/Chatter.Rest.UriTemplates/` to catch DI integration impact of core changes, `src/Directory.Build.props`, plus its own workflow file and the two reusable workflows `version-check.yml` and `create-version-tag.yml`) |
| Pull request | targeting `main` (same path scope) |
| Manual | `workflow_dispatch` — runs the build job only; deploy and tag never run for manual dispatches |

### Reusable workflows

| File | Purpose |
|---|---|
| `version-check.yml` | Checks that the csproj `<Version>` is strictly greater than **both** the latest release tag and the `<Version>` currently on `origin/main` (tags are created post-merge by the tag job, so `origin/main` can be ahead of the newest tag; the baseline is the maximum of the two). Runs on pull requests only. When no tags exist, the `origin/main` comparison alone applies. Accepts an optional `extra-src-path` input naming an additional path whose non-markdown changes also require a bump; both caller workflows pass `src/Directory.Build.props` so the pre-merge check examines the same source set as the deploy job's publish gate. |
| `create-version-tag.yml` | Creates an annotated git tag (`{prefix}/vX.Y.Z`) after a successful deploy. Runs on main push only. |

### Job structure (both workflows)

**build:** restore (`--locked-mode`), build (`-c Release`), test (package-scoped), pack (package-scoped), upload artifact.

**version-check:** calls `version-check.yml` with the package's `src/` directory plus `src/Directory.Build.props` (via `extra-src-path`) — the same source set the deploy job's publish gate diffs post-merge, so the two gates agree. The PR's `<Version>` must be strictly greater than both the latest release tag and the `<Version>` on `origin/main`, closing the window where a merged bump has not been tagged yet. Runs on pull requests only. Tag prefixes: `uritemplate` (core), `uritemplate-di` (DI).

**deploy:** runs only when `github.ref == 'refs/heads/main'` **and** `github.event_name == 'push'` — i.e., the push produced by merging to `main`; a `workflow_dispatch` run never reaches deploy. The job declares a job-level `concurrency` group scoped to the package and ref (`nuget-deploy-uritemplate-${{ github.ref }}` core, `nuget-deploy-uritemplate-di-${{ github.ref }}` DI) with `cancel-in-progress: false`, so two deploys for the same package never run simultaneously and a run is never cancelled mid-push. This is mutual exclusion only, **not** a FIFO queue — GitHub runs pending jobs in arbitrary order and a newly arrived pending job replaces an existing pending one — so merge ordering and version correctness are enforced by `version-check.yml` and the publish gate, not by the concurrency group. Downloads the build artifact, then a "Determine publish action" step (`publish-gate`) queries the nuget.org flat-container index for the `<Version>` being packed and picks one of three outcomes:

- version not yet published on nuget.org (a 404 for a never-published package counts as unpublished) — first a downgrade guard requires the version to be strictly greater than the highest stable version already on nuget.org (refusing to publish a downgrade that slipped past the pre-merge check), then push `*.nupkg` to NuGet.org via the `NUGET_API_KEY_CHATTER_URITEMPLATE` secret; `--skip-duplicate` is no longer used, so a genuine push failure fails the job. If the push itself returns a 409 Conflict (a same-version publish by another publisher slipped between the gate's index query and the push), the step re-checks the index: when the version is confirmed published it succeeds with a "published concurrently" warning so `tag` still runs; any other failure — including a 409 the index cannot confirm — remains fatal
- version already published and the push contains no non-markdown changes to the package's sources — skip the push with a notice; the job still succeeds, so `tag` runs
- version already published but the push did change package sources — fail with an error listing the changed files; the fix is a `<Version>` bump

The source set for that decision is the package's own `src/` directory plus `src/Directory.Build.props` — identical to what `version-check` examines pre-merge.

**tag:** calls `create-version-tag.yml`. Runs after a successful deploy, under the same `main`-push-only condition as deploy (so it also never runs for `workflow_dispatch`). Tag format: `uritemplate/vX.Y.Z` (core) or `uritemplate-di/vX.Y.Z` (DI).

### Workflow hardening

- All actions across the workflows are pinned to full commit SHAs (with trailing version comments).
- Checkouts in the package pipelines and `version-check.yml` set `persist-credentials: false`, so the workflow token is not written into the repository's git config. The exception within these pipelines is `create-version-tag.yml`, whose checkout keeps credentials because that job pushes the tag. (`codeql-analysis.yml`, a repository-scanning workflow outside these pipelines, uses the default checkout behavior.)
- The NuGet API key is scoped to the deploy job only, via job-level `env`.
- Each deploy job holds a per-package `concurrency` group with `cancel-in-progress: false` — mutual exclusion against simultaneous pushes of the same package, not an ordering guarantee (see the deploy description above).

## 7. Code Style

`.editorconfig` enforces:

- **Line endings:** CRLF
- **Indentation:** tabs
- **Braces:** Allman style (`csharp_new_line_before_open_brace=all`)
- **Namespaces:** file-scoped (silent preference)

All projects have `<Nullable>enable</Nullable>`.

### Behavioral contract wording

Canonical behavioral contracts (in `docs/usage.md` and in XML doc comments) must be keyed so that a guard, a throw site, or a future code change cannot falsify them:

1. State what the caller must guarantee (an obligation), not what the library will do.
2. Condition consequences on the failure actually occurring, so a guard that fires negates the premise rather than contradicting the sentence.
3. Write library promises as prescriptive commitments: a future code path that violates one is a bug against the contract, not a counterexample that falsifies the docs.
4. Mark example lists as explicitly non-exhaustive.
5. Mirrors (e.g. `README.md`) carry only the obligation or commitment sentence plus a link to the canonical `docs/usage.md` anchor, never a copy of the detail.

The check for every contract sentence: could someone add a validation check tomorrow that makes this sentence false? If yes, it is keyed wrong; reword it as an obligation, a premise-conditioned consequence, or a prescriptive commitment. Worked examples: [Values must be finite sequences](usage.md#values-must-be-finite-sequences) and [Exception message content](usage.md#exception-message-content).

### Behavioral contract keying — single source per primitive

The wording clauses above govern how a contract sentence is phrased. This rule governs where a contract fact may live. The two are orthogonal: a sentence can satisfy every wording clause and still violate this rule.

1. A behavior implemented in one shared internal step is documented at exactly one canonical anchored section in `docs/usage.md`, keyed to that step — not to any of the overloads that expose it.
2. Every API surface exposing that step — overload prose in `docs/usage.md`, XML doc comments, `README.md` mirrors — carries at most one sentence plus a link to that canonical anchor, never a copy of the detail.
3. A true sentence restated per-overload is a defect against this rule regardless of its content. Replication is the violation, so a review can flag it without first proving the restatement false or divergent.

Worked examples: [Variable materialization](usage.md#variable-materialization), alongside the two wording examples cited above.

**Enforcement note:** of the two rules above, one slice now has teeth. The `<exception>`-tag shape — at most one sentence carrying the throw condition (semicolon-joined clauses count as one sentence; `e.g.`/`i.e.` abbreviations and dotted references such as `§3.2.1` do not end one), optionally followed by a single trailing `See "…" in docs/usage.md` pointer sentence — is mechanically enforced against the core assembly's generated XML documentation by `XmlDocExceptionTagShapeTests` in `test/Chatter.Rest.UriTemplates.Tests/`. That test checks shape only, not whether a sentence is true. Everything else in this class — `<summary>` and `<remarks>` blocks, `docs/usage.md` overload prose, `README.md` and `docs/architecture.md` mirrors, and whether a canonical sentence actually matches the code — remains convention-only, enforced by review. The residual is tracked in issue #69.

## 8. Test Conventions

- **Framework:** xunit 2.9.x
- **Assertions:** FluentAssertions 6.x (used exclusively; no direct xunit `Assert` calls)
- **Coverage:** coverlet.msbuild
- **Test naming:** `Method_Scenario_Expected` — e.g., `Expand_WithUndefinedVariable_OmitsVariable`
- **Test data:** inline for every test class except `UriTemplateComplianceTests`, which reads JSON fixtures from `TestData/`. The core test csproj copies `uritemplate-test/*.json` (four files from the submodule) into `TestData/` at build time; the compliance tests read three of them — `spec-examples.json`, `extended-tests.json`, `negative-tests.json`. The fourth, `spec-examples-by-section.json`, is copied but not currently read (tracked in issue #56)
- **InternalsVisibleTo:** configured in `Chatter.Rest.UriTemplates.csproj` for two assemblies: the core test assembly (`Chatter.Rest.UriTemplates.Tests`) and the DI assembly (`Chatter.Rest.UriTemplates.DependencyInjection`). Internal types (e.g. `UriTemplateParser`, `UriTemplateExpander`, `UriTemplateFactory`; non-exhaustive) are accessible in both
