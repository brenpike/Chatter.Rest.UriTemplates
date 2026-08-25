# Contributing

Contributions are welcome. For non-trivial changes, please open an issue first to discuss what you would like to change.

## Reporting Issues

Use [GitHub Issues](https://github.com/brenpike/Chatter.Rest.UriTemplates/issues) to report bugs or request features. Please include:

- .NET SDK version
- Package version
- A minimal reproduction
- Actual vs expected behavior

## Getting Started

Prerequisites:

- .NET 8.0 SDK (8.0.x)
- No other tools required

Clone and build:

```bash
git clone --recurse-submodules https://github.com/brenpike/Chatter.Rest.UriTemplates.git
cd Chatter.Rest.UriTemplates
dotnet restore
dotnet build
```

If you already cloned without `--recurse-submodules`, run `git submodule update
--init` before running the tests. The `uritemplate-test` submodule carries the
official RFC 6570 suite that `UriTemplateComplianceTests` reads, and those tests
fail rather than skip when it is missing.

Run tests:

```bash
dotnet test
```

Full details on build, test, and pack commands are in [docs/development.md](docs/development.md).

## Making Changes

Branch naming:

- `feature/<short-description>` for new features
- `bugfix/<short-description>` for bug fixes
- `docs/<short-description>` for documentation
- `refactor/<short-description>` for refactoring

Always branch from `main` and submit pull requests back to `main`.

## Code Style

Follow the existing `.editorconfig` rules:

- Indentation: tabs
- Line endings: CRLF
- Braces: Allman style

Nullable reference types are enabled in every project (`<Nullable>enable</Nullable>` in each csproj, rather than an `.editorconfig` rule).

Run `dotnet build` before submitting. The `src/` projects treat warnings as errors, so a new warning fails the build. Two kinds of warnings are expected: the pre-existing `CA1305`/`CA1510`/`CA1716` findings tracked in `src/Directory.Build.props`, and occasionally `NU1900`, which NuGet emits when vulnerability data cannot be retrieved (audit-service outage or offline restore) — it is exempted permanently by design, is not a tracked code defect, and is not caused by your change.

## Tests

- All new behavior must include tests
- Framework: xunit 2.9.x with FluentAssertions 6.x
- Test naming: `Method_Scenario_Expected` (example: `Expand_WithUndefinedVariable_OmitsVariable`)

## Pull Request Checklist

- [ ] Branched from `main`
- [ ] `dotnet build` passes with no new warnings (expected: the pre-existing `CA1305`/`CA1510`/`CA1716` findings, plus `NU1900` if the NuGet audit service was unreachable during restore — that one is environmental and not your fault)
- [ ] `dotnet test` passes
- [ ] New behavior has test coverage
- [ ] Code style matches `.editorconfig`
- [ ] PR description explains the why, not just the what

## License

By contributing, you agree your contributions will be licensed under the project's [MIT License](LICENSE).

## Claude Code Plugins

The repository's checked-in `.claude/settings.json` enables four third-party Claude Code plugins for every contributor who trusts the workspace:

- `claude-mem@thedotmack`
- `hivemind@brenpike`
- `caveman@caveman`
- `codex@openai-codex`

The same file also sets `agent` to `hivemind:overlord`, making that plugin's
orchestrator the default agent for sessions opened in this workspace, and
registers a `SubagentStart` hook from `.claude/hooks/`.

Its `permissions.allow` list is deliberately limited to `Edit` rules scoped to
the gitignored `.hivemind/` directory. Pre-approved `Bash` rules are kept out of
the checked-in settings: a prefix rule such as `Bash(printf *)` also matches the
same command with shell redirection appended, so it would authorize writes
anywhere on a contributor's machine. Approve read-only commands per session, or
add them to your own gitignored `.claude/settings.local.json` if you want them
pre-approved on your machine only.

Plugins run hooks and agents at your local privilege level, so an update published by a plugin author executes on contributor machines that may hold NuGet and GitHub credentials.

### Why the plugins are not version-pinned

Claude Code's `enabledPlugins` setting only accepts booleans - there is no per-repository version or commit pin for a plugin enabled from someone else's marketplace. Exact pinning (`sha`, `ref`, or npm `version`) is only expressible inside a marketplace's own plugin entries, which this repository does not author. Re-declaring the marketplaces inline in `.claude/settings.json` with pinned SHAs was considered and rejected: Claude Code only registers marketplaces it does not already know, so the pin would silently not apply on machines where those marketplaces are already registered, giving a false sense of safety.

Two mitigating behaviors reduce the exposure:

- Auto-update defaults to off for third-party marketplaces, so an installed plugin stays at its installed version on each machine until someone updates it deliberately.
- Claude Code prompts for workspace trust before honoring the repository's plugin settings.

### Residual risk and update review

Treat plugin updates like dependency updates:

- Do not run `claude plugin update` for these plugins casually. Review the plugin repository's diff between your installed version and the new one first, paying attention to hooks, agents, and MCP server definitions.
- The repository owner, Brennan Pike ([@brenpike](https://github.com/brenpike)), reviews and approves plugin version changes and any change to `enabledPlugins` in `.claude/settings.json`. Raise an issue before proposing one.
- If you are not comfortable running the plugins, disable them for your machine in `.claude/settings.local.json` (gitignored). The flags must be nested inside an `enabledPlugins` object — Claude Code only reads plugin flags from that object, and unknown top-level keys are ignored:

  ```json
  {
    "enabledPlugins": {
      "claude-mem@thedotmack": false,
      "hivemind@brenpike": false,
      "caveman@caveman": false,
      "codex@openai-codex": false
    }
  }
  ```

  Local project settings take precedence over the repository's `.claude/settings.json`, and `enabledPlugins` entries apply per individual plugin, so a local `false` overrides the shared `true` for that plugin without restating anything else. This is the opt-out mechanism the [Claude Code settings reference](https://code.claude.com/docs/en/settings-reference#enabledplugins) documents: "To opt out of a project-enabled plugin on your machine, set it to `false` in `.claude/settings.local.json`." The library builds and tests without the plugins.

