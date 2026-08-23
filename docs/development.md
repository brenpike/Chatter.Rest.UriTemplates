# Development Guide

## 1. Prerequisites

- **.NET 8.0 SDK (8.0.x)** — pinned in `global.json` with `rollForward: latestMinor`
- **Language:** C# 10.0 (set per project via `<LangVersion>10.0</LangVersion>`)
- No external tools required beyond the .NET SDK

## 2. Solution Structure

```
Chatter.Rest.UriTemplates.sln             <- solution file (repo root)
src/
  Chatter.Rest.UriTemplates/
    Chatter.Rest.UriTemplates.csproj     <- targets net8.0;netstandard2.0
    UriTemplate.cs                        <- public entry point
    IUriTemplateParser.cs
    IUriTemplateFactory.cs
    UriTemplateToken.cs
    UriTemplateOperator.cs
    UriTemplateVarSpec.cs
    UriTemplateParser.cs
    UriTemplateExpander.cs
    UriTemplateFactory.cs
  Chatter.Rest.UriTemplates.DependencyInjection/
    Chatter.Rest.UriTemplates.DependencyInjection.csproj  <- targets net8.0;netstandard2.0
    ServiceCollectionExtensions.cs        <- AddUriTemplates() extension method
test/
  Chatter.Rest.UriTemplates.Tests/
    Chatter.Rest.UriTemplates.Tests.csproj  <- targets net8.0
    UriTemplateLevel1Tests.cs
    UriTemplateLevel2Tests.cs
    UriTemplateLevel3Tests.cs
    UriTemplateLevel4Tests.cs
    UriTemplateEdgeCaseTests.cs
    UriTemplateGetVariablesTests.cs
  Chatter.Rest.UriTemplates.DependencyInjection.Tests/
    Chatter.Rest.UriTemplates.DependencyInjection.Tests.csproj  <- targets net8.0
    ServiceCollectionExtensionsTests.cs
```

**Target frameworks:**

- `src/` project multi-targets `net8.0;netstandard2.0`
- `test/` project targets `net8.0` only

## 3. Build Commands

```bash
# Restore (locked mode - requires packages.lock.json)
dotnet restore --locked-mode

# Build (Debug)
dotnet build

# Build (Release, skip restore)
dotnet build -c Release --no-restore
```

**Lock file note:** if `dotnet restore --locked-mode` fails, delete `packages.lock.json` and run `dotnet restore` to regenerate. Commit the regenerated file.

**Warnings:** zero warnings expected on `net8.0`. The `netstandard2.0` target may emit baseline nullable-context warnings (`CS8604`/`CS8603`) — these are pre-existing and not regressions.

## 4. Test Commands

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

**Test classes:**

- `UriTemplateLevel1Tests` — simple string expansion
- `UriTemplateLevel2Tests` — reserved and fragment expansion
- `UriTemplateLevel3Tests` — multi-variable and operator expansion
- `UriTemplateLevel4Tests` — prefix modifiers, list values, associative arrays, explode behavior
- `UriTemplateEdgeCaseTests` — malformed templates, mixed-level, modifier validation
- `UriTemplateGetVariablesTests` — variable enumeration across all operator types

## 5. NuGet Packaging

```bash
dotnet pack src/Chatter.Rest.UriTemplates/Chatter.Rest.UriTemplates.csproj -c Release -o publish/nuget
```

Output lands in `publish/nuget/`. Package ID: `Chatter.Rest.UriTemplates` v0.3.0.

To pack the DI extension package:

```bash
dotnet pack src/Chatter.Rest.UriTemplates.DependencyInjection/Chatter.Rest.UriTemplates.DependencyInjection.csproj -c Release -o publish/nuget
```

Package ID: `Chatter.Rest.UriTemplates.DependencyInjection` v0.1.1.

## 6. CI/CD Parity

Two per-project workflows in `.github/workflows/`. Each workflow covers one NuGet package independently.

### Core package — `uritemplate-cicd.yml`

| Trigger | Condition |
|---|---|
| Push | `feature/**`, `bugfix/**`, `hotfix/**`, `refactor/**`, `chore/**`, `docs/**`, `test/**`, `ci/**`, `main` (path-scoped to `src/Chatter.Rest.UriTemplates/`, `src/Directory.Build.props`, `test/Chatter.Rest.UriTemplates.Tests/`, and the workflow files themselves) |
| Pull request | targeting `main` (same path scope) |
| Manual | `workflow_dispatch` — runs the build job only; deploy and tag never run for manual dispatches |

### DI package — `uritemplate-di-cicd.yml`

| Trigger | Condition |
|---|---|
| Push | same branch patterns (path-scoped to `src/Chatter.Rest.UriTemplates.DependencyInjection/`, `test/Chatter.Rest.UriTemplates.DependencyInjection.Tests/`, `src/Chatter.Rest.UriTemplates/` to catch DI integration impact of core changes, `src/Directory.Build.props`, and the workflow files themselves) |
| Pull request | targeting `main` (same path scope) |
| Manual | `workflow_dispatch` — runs the build job only; deploy and tag never run for manual dispatches |

### Reusable workflows

| File | Purpose |
|---|---|
| `version-check.yml` | Checks that the csproj `<Version>` is strictly greater than the latest release tag. Runs on pull requests only. When no tags exist, falls back to comparing against the origin/main csproj version. Accepts an optional `extra-src-path` input naming an additional path whose non-markdown changes also require a bump; both caller workflows pass `src/Directory.Build.props` so the pre-merge check examines the same source set as the deploy job's publish gate. |
| `create-version-tag.yml` | Creates an annotated git tag (`{prefix}/vX.Y.Z`) after a successful deploy. Runs on main push only. |

### Job structure (both workflows)

**build:** restore (`--locked-mode`), build (`-c Release`), test (package-scoped), pack (package-scoped), upload artifact.

**version-check:** calls `version-check.yml` with the package's `src/` directory plus `src/Directory.Build.props` (via `extra-src-path`) — the same source set the deploy job's publish gate diffs post-merge, so the two gates agree. Runs on pull requests only. Tag prefixes: `uritemplate` (core), `uritemplate-di` (DI).

**deploy:** runs only when `github.ref == 'refs/heads/main'` **and** `github.event_name == 'push'` — i.e., the push produced by merging to `main`; a `workflow_dispatch` run never reaches deploy. Downloads the build artifact, then a "Determine publish action" step (`publish-gate`) queries the nuget.org flat-container index for the `<Version>` being packed and picks one of three outcomes:

- version not yet published on nuget.org (a 404 for a never-published package counts as unpublished) — push `*.nupkg` to NuGet.org via the `NUGET_API_KEY_CHATTER_URITEMPLATE` secret; `--skip-duplicate` is no longer used, so a genuine push failure fails the job
- version already published and the push contains no non-markdown changes to the package's sources — skip the push with a notice; the job still succeeds, so `tag` runs
- version already published but the push did change package sources — fail with an error listing the changed files; the fix is a `<Version>` bump

The source set for that decision is the package's own `src/` directory plus `src/Directory.Build.props` — identical to what `version-check` examines pre-merge.

**tag:** calls `create-version-tag.yml`. Runs after a successful deploy, under the same `main`-push-only condition as deploy (so it also never runs for `workflow_dispatch`). Tag format: `uritemplate/vX.Y.Z` (core) or `uritemplate-di/vX.Y.Z` (DI).

### Workflow hardening

- All actions across the workflows are pinned to full commit SHAs (with trailing version comments).
- Checkouts set `persist-credentials: false`, so the workflow token is not written into the repository's git config. The one exception is `create-version-tag.yml`, whose checkout keeps credentials because that job pushes the tag.
- The NuGet API key is scoped to the deploy job only, via job-level `env`.

## 7. Code Style

`.editorconfig` enforces:

- **Line endings:** CRLF
- **Indentation:** tabs
- **Braces:** Allman style (`csharp_new_line_before_open_brace=all`)
- **Namespaces:** file-scoped (silent preference)

All projects have `<Nullable>enable</Nullable>`.

## 8. Test Conventions

- **Framework:** xunit 2.4.x
- **Assertions:** FluentAssertions 6.x (preferred); xunit `Assert` also used
- **Coverage:** coverlet.msbuild
- **Test naming:** `Method_Scenario_Expected` — e.g., `Expand_WithUndefinedVariable_OmitsVariable`
- **Test data:** all inline (no JSON fixture files)
- **InternalsVisibleTo:** configured in `Chatter.Rest.UriTemplates.csproj` — internal types (`UriTemplateParser`, `UriTemplateExpander`, `UriTemplateFactory`) are accessible in tests
