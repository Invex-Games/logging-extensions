# Agent Instructions

Guidance for AI agents working in **Invex Logging Extensions** — a small, focused set of utilities for
`Microsoft.Extensions.Logging`: a file provider with size- and time-based rollover, archive cleanup
thresholds, and scoped group/level routing; a Spectre.Console provider; and startup logging helpers.
Keep changes focused and defer to the linked docs for detail.

## What's in the repo

| Project | Role | Target frameworks |
|---------|------|-------------------|
| `Invex.Extensions.Logging.File` | The library: `FileLoggerExtension.AddFile`, `FileLoggerConfiguration`, `FileRolloverInterval`, plus internal providers/writers | `net10.0;net9.0;net8.0;netstandard2.0` |
| `Invex.Extensions.Logging.File.Tests` | NUnit test suite, including a public API surface snapshot test | `net10.0;net9.0;net8.0;net48` |
| `Invex.Extensions.Logging.FancyConsole` | Spectre.Console console provider: `AddFancyConsole`, `FancyConsoleLoggerConfiguration`, `FancyConsoleLayout`, `FancyConsoleExceptionFormat` | `net10.0;net9.0;net8.0;netstandard2.0` |
| `Invex.Extensions.Logging.FancyConsole.Tests` | NUnit tests for layouts, styles, exceptions, provider behavior, and public API | `net10.0;net9.0;net8.0;net48` |
| `Invex.Extensions.Logging.Utils` | Standalone host loggers, factory ownership, and startup information: `LogUtil` and `HostLogger` | `net10.0;net9.0;net8.0;netstandard2.0` |
| `Invex.Extensions.Logging.Utils.Tests` | NUnit tests for startup information, factory filtering and ownership, wrapper delegation, and public API | `net10.0;net9.0;net8.0;net48` |
| `_atom` | Atom build definition (`IBuild.cs`) that generates the GitHub Actions workflows | `net10.0` |

Sources live under `src/`, tests under `tests/`, the Atom build definition under `_atom/`, and the
DocFX documentation site is configured by `docfx.json` with content in `docs/`, `api/`, `index.md`,
and `toc.yml`.

## Build & language specifics

- **.NET 10 SDK or a later stable SDK** is required (see `global.json`, which permits major-version roll-forward). The libraries multi-target down to
  `netstandard2.0` (via `Polyfill`, `Microsoft.Bcl.TimeProvider`, and `System.Threading.Channels`);
  tests also run on `net48`.
- C# `LangVersion` 14, `ImplicitUsings` and `Nullable` enabled, `TreatWarningsAsErrors` on.
- Global usings live in each project's `_usings.cs` — add shared usings there, not per-file.
  The library's `_usings.cs` also declares `InternalsVisibleTo` for the test project.
- `GenerateDocumentationFile` is on. `CS1591` is in the repo-wide `NoWarn`, but the convention is
  still to fully XML-document **all** types and members, public and internal — match the existing
  style.
- Framework-specific code uses `#if NET8_0_OR_GREATER` guards (mostly nullability differences on
  the older targets); preserve both branches when editing.

Build and test the whole solution:

```shell
dotnet build Invex.Extensions.Logging.slnx
dotnet test Invex.Extensions.Logging.slnx
```

Build the docs site:

```shell
dotnet build Invex.Extensions.Logging.slnx --configuration Release
docfx docfx.json          # add --serve to preview locally
```

DocFX reads the three libraries' `bin/Release/net10.0` assemblies and sibling XML documentation files.
Build them before regenerating docs. Compiled metadata preserves registration methods declared in
C# 14 extension blocks, which source extraction in DocFX 2.78.5 omits. Generated `api/*.yml` files are
ignored by Git; edit source XML comments or Markdown instead.

After C# changes, run ReSharper cleanup over the solution. Resolve the SDK selected by `global.json`
and pass its `MSBuild.dll`; this avoids ReSharper selecting an incompatible Visual Studio MSBuild:

```powershell
$sdk = dotnet --version
jb cleanupcode Invex.Extensions.Logging.slnx --include="**.cs" --toolset-path="C:\Program Files\dotnet\sdk\$sdk\MSBuild.dll"
```

If `jb` is unavailable, install it with `dotnet tool install -g JetBrains.ReSharper.GlobalTools`.
Cleanup honors `.editorconfig` and repository/team-shared `*.DotSettings` automatically.

## Repository layout

- `src/Invex.Extensions.Logging.File/` contains the library and its public configuration surface.
- `tests/Invex.Extensions.Logging.File.Tests/` contains NUnit tests, test doubles, and Verify snapshots.
- `src/Invex.Extensions.Logging.FancyConsole/` and `tests/Invex.Extensions.Logging.FancyConsole.Tests/`
  contain the Spectre.Console console logger and its tests.
- `src/Invex.Extensions.Logging.Utils/` contains startup logging helpers; the sample under `samples/`
  demonstrates their use alongside the providers.
- `tests/Invex.Extensions.Logging.Utils.Tests/` covers utility formatting, structured startup state,
  standalone factory filtering and disposal, wrapper delegation, and the public API snapshot.
- `_atom/` contains the Atom build definition.
- `docs/`, `README.md`, `index.md`, `toc.yml`, and `docfx.json` define the DocFX site.
- `.github/workflows/` and `.github/dependabot.yml` are generated or maintained from the Atom definition
  and should not be hand-edited when Atom owns the change.

## Architecture overview

The file provider's public surface is intentionally tiny — three types:

- **`FileLoggerExtension`** (`Invex.Extensions.Logging.File`) — `AddFile(ILoggingBuilder, bool)` and
  `AddFile(ILoggingBuilder, Action<FileLoggerConfiguration>, bool)`. The `buffered` flag selects
  which provider gets registered. `BeginGroupScope(ILogger, string)` adds a standard scope containing
  the `"Group"` property for routing.
- **`FileLoggerConfiguration`** (`...File.Configuration`) — options class bound to the
  `Logging:File` section (provider alias `File`), with `Default*` constants for fixed scalar options.
  `LogName` defaults to `AppDomain.CurrentDomain.FriendlyName` and has no default constant.
  `PerGroupLogName` and `PerLevelLogName` default to empty dictionaries without constants.
- **`FileRolloverInterval`** (`...File.Configuration`) — time-based rollover enum.

Everything else is `internal`:

- **`FileLoggerProvider`** (abstract) caches one `FileLogger` per category and tracks config via
  `IOptionsMonitor<T>` and implements `ISupportExternalScope`; **`BufferedFileLoggerProvider`** /
  **`DirectFileLoggerProvider`** supply the writer. Both providers carry `[ProviderAlias("File")]` and expose static `FileSystem`
  (`System.IO.Abstractions.IFileSystem`) and `TimeProvider` hooks that tests replace.
- **`FileLogger`** formats entries (`[{timestamp} {level-code} {category}] {message}`) and forwards
  to an **`IFileLogWriter`**.
- **`BufferedFileLogWriter`** queues entries on an unbounded `Channel` drained by a dedicated
  background thread in batches of up to 10; **`DirectFileLogWriter`** writes synchronously on the
  calling thread. **`FileLogWriterUtil`** holds the shared rollover/purge/append logic.

### Behavioral contracts (do not break these)

- **Rollover**: size-based when the active file would reach `FileSizeLimitBytes`; time-based when
  elapsed time since file creation meets `RolloverInterval` (elapsed durations, **not** calendar
  boundaries — `Month` = 30 days, `Year` = 365 days). Rolled files are named
  `{resolvedName}_{yyMMdd-HHmmss}.log` with `_{n}` collision suffixes; `resolvedName` includes any
  group and level suffixes.
- **Retention**: on each rollover/new file, if archives for the exact resolved base name total
  ≥ `MaxTotalSizeBytes`, at most one oldest archive is deleted; this is not a hard disk quota. Archive names must end in `_yyMMdd-HHmmss`,
  optionally a positive numeric collision suffix, and `.log`. Configured active destinations are
  excluded from purging and rollover-name selection. Purging is per resolved base name.
- **Per-level routing**: `PerLevelLogName` values are suffixes appended to `LogName` after any group
  suffix, separated by underscores. `LogName` is non-nullable and defaults to
  `AppDomain.CurrentDomain.FriendlyName`. `SetLogNameSuffix` replaces it with
  `{AppDomain.CurrentDomain.FriendlyName}_{suffix}`; an empty suffix leaves a trailing underscore.
  Missing, null, or empty mapped suffixes add no text or separator. This intentionally replaces the
  earlier behavior where level mappings supplied replacement base names.
- **Group routing**: `PerGroupLogName` maps the innermost nonempty string `"Group"` scope value.
  Its nonempty mapped suffix follows the base name before the level suffix:
  `{baseName}[_{groupSuffix}][_{levelSuffix}]`. Null or empty mapped suffixes are omitted. An unmapped
  group uses level/default routing, even inside a mapped outer group. Capture the
  group during `Log`, then resolve mappings from current configuration per write/batch.
- **File write/routing resilience**: file operations make an initial attempt plus up to five retries,
  echo failures to console/debug output, then drop the affected batch/entry without propagating those
  failures to the app. Custom formatter and timestamp operations run before this handling and can throw.
  Keep writer resilience intact.
- **No level filtering in the provider** (`IsEnabled` returns `true`); filtering belongs to the
  framework. Scopes support group routing but are not included in log lines. Empty messages are skipped.
- **Runtime config reload** must keep working — config is re-read per write/batch via
  `IOptionsMonitor`.
- Buffered and direct modes must remain behaviorally identical apart from threading/durability;
  if you change the write pipeline in one writer, mirror it in the other (and prefer pushing
  shared logic into `FileLogWriterUtil`).

### FancyConsole library

`Invex.Extensions.Logging.FancyConsole` is a Spectre.Console console provider. Public surface:
`FancyConsoleLoggerExtensions.AddFancyConsole` (two overloads), `FancyConsoleLoggerConfiguration`
(bound from `Logging:FancyConsole`, alias `FancyConsole`), `FancyConsoleLayout`, and
`FancyConsoleExceptionFormat`. Internally, `FancyConsoleLoggerProvider` caches loggers, tracks config
via `IOptionsMonitor`, captures scopes, and routes entries at/above `LogToStandardErrorThreshold` to
stderr; `FancyConsoleLogger` builds a `FancyConsoleLogEntry` and never throws; `FancyConsoleFormatter`
holds all layout/exception rendering. The provider's static `Console`, `ErrorConsole`, and
`TimeProvider` hooks are replaced by tests. `Pretty` exceptions fall back to `Full` when Spectre cannot
render them (e.g. unthrown exceptions on .NET Framework). See `docs/fancy-console.md` for exact output.

### Logging utilities

`Invex.Extensions.Logging.Utils` exposes `LogUtil` and `HostLogger`. `CreateHostLoggerFactory` registers
a `Microsoft` category filter at `Warning`, then invokes an optional configuration callback; it adds no
providers by default and does not automatically load host configuration. `CreateHostLogger` wraps a
logger with category `Host` and owns its factory. Disposing `HostLogger` disposes that factory and its
owned providers. `LogStartupInfo<T>` uses `typeof(T).Assembly`, the machine name, and an optional host
environment, and logs at Information. See `docs/logging-utilities.md` for current output and lifetime rules.

## Key design rules

- Keep the public surface minimal; new functionality should usually be `internal` with public
  exposure only via `FileLoggerConfiguration` options or `AddFile` parameters.
- New options belong on `FileLoggerConfiguration` as properties with a matching `Default*`
  constant and a default that preserves existing behavior. Dictionary options use empty dictionaries
  without constants, following `PerLevelLogName` and `PerGroupLogName`.
- All file system access goes through `System.IO.Abstractions` (`IFileSystem`) and all time access
  through `TimeProvider` — never use `System.IO.File`/`DateTime.Now` directly (analyzers enforce
  this). This is what makes the test suite possible.

## Atom workflows

The GitHub Actions workflow YAML under `.github/workflows/` (`Validate.yml`, `Build.yml`,
`Dependabot Enable auto-merge.yml`, `Cleanup Prereleases.yml`) is **generated** from the Atom
build definition in `_atom/IBuild.cs`.

Whenever you change anything that affects the workflows — targets, workflow definitions, triggers,
options, or params/secrets — regenerate the YAML:

```shell
atom gen
```

(equivalently `dotnet run --project _atom -- gen`). Commit the regenerated `.github/workflows/`
files alongside your `_atom/` changes; never hand-edit the generated YAML.

A drift between `_atom/IBuild.cs` and the committed YAML should be treated as a missing
`atom gen` run.

Note that CI tests run on a matrix of `net8.0`/`net9.0`/`net10.0` × Ubuntu/Windows, plus a
Windows-only `net48` job (`TestFxProjects`) — keep all target frameworks green.

## Checklist: making a change

1. Follow existing patterns and make precise, focused changes.
2. Keep the public API minimal; add XML documentation and `[PublicAPI]` to new public surface.
3. Update both buffered and direct paths when changing write behavior, preferably in shared writer logic.
4. Add or update tests, then run `dotnet build` and `dotnet test` for the solution.
5. Run `jb cleanupcode` with the SDK `MSBuild.dll` as shown above.
6. Update `README.md` and the relevant `docs/` page for consumer-facing behavior.
7. If generated workflows or their inputs changed, run `atom gen` and commit the generated files.

## Conventions

- Annotate every new public type with `[PublicAPI]` — the in-repo analyzer flags anything missing,
  and warnings are errors.
- Add XML doc comments to all types and members (public *and* internal). Match the existing
  `<summary>` / `<param>` / `<remarks>` style, and keep docs **accurate to the implementation**
  (e.g. exact rollover semantics, retry counts, naming schemes).
- Use Conventional Commits — the prefix drives versioning (GitVersion):

  | Prefix | Version bump |
  |--------|--------------|
  | `breaking:` / `major:` | Major |
  | `feat:` / `feature:` / `minor:` | Minor |
  | `fix:` / `patch:` | Patch |
  | `semver-none` / `semver-skip` | No bump |

- When adding user-facing features, update the relevant `docs/` page and `README.md`. The README
  is the DocFX site home page and is packed into the NuGet package.

## Testing & the Verify workflow

- Tests use **NUnit** with **Shouldly**, **FakeItEasy**, **Verify** (`Verify.NUnit`), and
  **`System.IO.Abstractions.TestingHelpers`** (`MockFileSystem`).
- `TestBase` builds a real Generic Host with `AddFile`, and tests inject a `MockFileSystem` and
  `TestTimeProvider` via the static `FileSystem` / `TimeProvider` setters on
  `BufferedFileLoggerProvider` / `DirectFileLoggerProvider`. No test should touch the real disk
  or clock.
- Buffered-mode tests must call `TestBase.StopApp()` (which disposes the host) before asserting
  file contents, so the background writer flushes.
- A snapshot test fails when its output differs from the committed `*.verified.txt`. On failure,
  Verify writes a `*.received.txt` next to it.
- If the diff is unintended, fix the code. If the change is valid (expected new output), accept
  it and re-run:
  1. Overwrite the `*.verified.txt` with the contents of the matching `*.received.txt`.
  2. Delete the `*.received.txt`.
  3. Re-run `dotnet test` to confirm the suite is green.
- `PublicApiTests.VerifyPublicApiSurface.verified.txt` tracks the **complete public API**. An
  unexpected diff there signals an unintentional API change — treat it as such and double-check
  before accepting. The Validate workflow's `CheckPrForBreakingChanges` target inspects changes
  to `tests/**/*.verified.txt` on PRs, so API-surface changes must be intentional and committed.

## Adding a new option to `FileLoggerConfiguration`

1. Add the property plus a `Default*` constant (except dictionary options), with a default that
   preserves current behavior, and full XML docs.
2. Honor it in **both** `BufferedFileLogWriter` and `DirectFileLogWriter` (or in
   `FileLogWriterUtil` if the logic is shared).
3. Add unit tests covering both buffered and direct modes, using `MockFileSystem` /
   `TestTimeProvider`.
4. Update `PublicApiTests.VerifyPublicApiSurface.verified.txt` (see the Verify workflow above).
5. Document it in `docs/configuration.md` (and `docs/rollover-and-retention.md` if it affects
   rollover/retention), plus the README options example if user-facing.

## Defer to the docs

For anything beyond the above, prefer these over duplicating detail:

- `README.md` — package overview, quick start, and configuration examples.
- `docs/getting-started.md` — installation, registration, defaults, level filtering.
- `docs/logging-utilities.md` — standalone host logging, configuration, startup information, and factory lifetime.
- `docs/configuration.md` — every option with defaults, JSON/code examples, group/level routing.
- `docs/rollover-and-retention.md` — file naming, rollover semantics, purging, sizing guidance.
- `docs/buffering.md` — buffered vs. direct trade-offs and error handling.
- `docs/log-format.md` — the exact log line format and parsing guidance.
- `docs/fancy-console.md` — FancyConsole layouts, options, exceptions, and output behavior.
- `api/index.md` — entry point to the generated API reference.
