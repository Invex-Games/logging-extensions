# Copilot Instructions

Guidance for AI agents working in **Invex Logging Extensions** — a small, focused set of utilities for
`Microsoft.Extensions.Logging`, currently consisting of a file logger provider with size- and time-based
rollover, disk usage caps, and per-level log file routing. Keep changes focused and defer to the linked
docs for detail.

## What's in the repo

| Project | Role | Target frameworks |
|---------|------|-------------------|
| `Invex.Extensions.Logging.File` | The library: `FileLoggerExtension.AddFile`, `FileLoggerConfiguration`, `FileRolloverInterval`, plus internal providers/writers | `net10.0;net9.0;net8.0;netstandard2.0` |
| `Invex.Extensions.Logging.File.Tests` | NUnit test suite, including a public API surface snapshot test | `net10.0;net9.0;net8.0;net48` |
| `_atom` | Atom build definition (`IBuild.cs`) that generates the GitHub Actions workflows | `net10.0` |

Sources live under `src/`, tests under `tests/`, the Atom build definition under `_atom/`, and the
DocFX documentation site is configured by `docfx.json` with content in `docs/`, `api/`, `index.md`,
and `toc.yml`.

## Build & language specifics

- **.NET 10 SDK** is required (see `global.json`). The library multi-targets down to
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
docfx docfx.json          # add --serve to preview locally
```

## Architecture overview

The public surface is intentionally tiny — three types:

- **`FileLoggerExtension`** (`Invex.Extensions.Logging.File`) — `AddFile(ILoggingBuilder, bool)` and
  `AddFile(ILoggingBuilder, Action<FileLoggerConfiguration>, bool)`. The `buffered` flag selects
  which provider gets registered.
- **`FileLoggerConfiguration`** (`...File.Configuration`) — options class bound to the
  `Logging:File` section (provider alias `File`), with `Default*` constants for every option.
- **`FileRolloverInterval`** (`...File.Configuration`) — time-based rollover enum.

Everything else is `internal`:

- **`FileLoggerProvider`** (abstract) caches one `FileLogger` per category and tracks config via
  `IOptionsMonitor<T>`; **`BufferedFileLoggerProvider`** / **`DirectFileLoggerProvider`** supply the
  writer. Both providers carry `[ProviderAlias("File")]` and expose static `FileSystem`
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
  `{LogName}_{yyMMdd-HHmmss}.log` with `_{n}` collision suffixes.
- **Retention**: on each rollover/new file, if rolled-over files matching `{LogName}_*.log` total
  ≥ `MaxTotalSizeBytes`, the oldest one is deleted. The active file is never purged. Purging is
  per base name.
- **Per-level routing**: levels present in `PerLevelLogName` write to that file name; `null`
  (whether the dictionary value or `LogName`) falls back to `AppDomain.CurrentDomain.FriendlyName`.
- **Logging never throws into the app**: file writes retry up to five times, echoing failures to
  console/debug output, then drop the batch/entry. Keep this resilience intact.
- **No level filtering in the provider** (`IsEnabled` returns `true`); filtering belongs to the
  framework. Scopes are unsupported (`BeginScope` returns `null`). Empty messages are skipped.
- **Runtime config reload** must keep working — config is re-read per write/batch via
  `IOptionsMonitor`.
- Buffered and direct modes must remain behaviorally identical apart from threading/durability;
  if you change the write pipeline in one writer, mirror it in the other (and prefer pushing
  shared logic into `FileLogWriterUtil`).

## Key design rules

- Keep the public surface minimal; new functionality should usually be `internal` with public
  exposure only via `FileLoggerConfiguration` options or `AddFile` parameters.
- New options belong on `FileLoggerConfiguration` as properties with a matching `Default*`
  constant and a default that preserves existing behavior.
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

1. Add the property plus a `Default*` constant, with a default that preserves current behavior,
   and full XML docs.
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
- `docs/configuration.md` — every option with defaults, JSON/code examples, per-level routing.
- `docs/rollover-and-retention.md` — file naming, rollover semantics, purging, sizing guidance.
- `docs/buffering.md` — buffered vs. direct trade-offs and error handling.
- `docs/log-format.md` — the exact log line format and parsing guidance.
- `api/index.md` — entry point to the generated API reference.

