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
git clone https://github.com/brenpike/Chatter.Rest.UriTemplates.git
cd Chatter.Rest.UriTemplates
dotnet restore
dotnet build
```

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
- Nullable: enabled

Run `dotnet build` before submitting - zero warnings expected on `net8.0`.

## Tests

- All new behavior must include tests
- Framework: xunit 2.9.x with FluentAssertions 6.x
- Test naming: `Method_Scenario_Expected` (example: `Expand_WithUndefinedVariable_OmitsVariable`)

## Pull Request Checklist

- [ ] Branched from `main`
- [ ] `dotnet build` passes with zero warnings on `net8.0`
- [ ] `dotnet test` passes
- [ ] New behavior has test coverage
- [ ] Code style matches `.editorconfig`
- [ ] PR description explains the why, not just the what

## License

By contributing, you agree your contributions will be licensed under the project's [MIT License](LICENSE).

## Claude Code Plugins

The repository's checked-in `.claude/settings.json` enables two third-party Claude Code plugins for every contributor who trusts the workspace:

- `claude-mem@thedotmack`
- `agent-framework@brenpike`

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
      "agent-framework@brenpike": false
    }
  }
  ```

  Local project settings take precedence over the repository's `.claude/settings.json`, and `enabledPlugins` entries apply per individual plugin, so a local `false` overrides the shared `true` for that plugin without restating anything else. This is the opt-out mechanism the [Claude Code settings reference](https://code.claude.com/docs/en/settings-reference#enabledplugins) documents: "To opt out of a project-enabled plugin on your machine, set it to `false` in `.claude/settings.local.json`." The library builds and tests without the plugins.

  Note: the `agent-framework` plugin's source repository has been renamed to [brenpike/hivemind](https://github.com/brenpike/hivemind), whose marketplace publishes the successor plugin under the name `hivemind`. If your machine has that successor installed, its identifier is `hivemind@brenpike` — add `"hivemind@brenpike": false` to the same object to disable it.
