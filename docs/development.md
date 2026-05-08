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
    UriTemplateOperator.cs
    UriTemplateExpression.cs
    UriTemplateParser.cs
    UriTemplateExpander.cs
  Chatter.Rest.UriTemplates.DependencyInjection/
    Chatter.Rest.UriTemplates.DependencyInjection.csproj  <- targets net8.0;netstandard2.0
    UriTemplateServiceCollectionExtensions.cs             <- public DI registration helpers
test/
  Chatter.Rest.UriTemplates.Tests/
    Chatter.Rest.UriTemplates.Tests.csproj  <- targets net8.0
    UriTemplateLevel1Tests.cs
    UriTemplateLevel2Tests.cs
    UriTemplateLevel3Tests.cs
    UriTemplateLevel4Tests.cs
    UriTemplateEdgeCaseTests.cs
    UriTemplateGetVariablesTests.cs
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
dotnet build -c Release --no-restore
dotnet test test/Chatter.Rest.UriTemplates.Tests/Chatter.Rest.UriTemplates.Tests.csproj -c Release --no-build
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

Single workflow in `.github/workflows/uritemplate-cicd.yml`.

| Trigger | Condition |
|---|---|
| Push | `feature/**` branches (path-scoped to `src/` and `test/`) |
| Push | `main` |
| Pull request | targeting `main` (path-scoped) |
| Manual | `workflow_dispatch` |

**Build job:** restore (`--locked-mode`), build (`-c Release`), test, pack, upload artifact.

**Deploy job:** runs only when `github.ref == 'refs/heads/main'` (after successful PR merge). Pushes package to NuGet.org via `NUGET_API_KEY_CHATTER_URITEMPLATE` secret.

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
- **InternalsVisibleTo:** configured in `Chatter.Rest.UriTemplates.csproj` — internal types (`UriTemplateParser`, `UriTemplateExpander`, `UriTemplateExpression`, `UriTemplateOperator`) are accessible in tests
