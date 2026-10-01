# Fancy Console Logger

`Invex.Extensions.Logging.FancyConsole` is a colorful console logger provider built on
[Spectre.Console](https://spectreconsole.net/). It offers several layouts, per-level styles, optional
scopes, styled or "pretty" exceptions, and standard-error routing, all configurable through the standard
options pipeline with runtime reload.

The library targets `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`.

## Registration

```csharp
using Invex.Extensions.Logging.FancyConsole;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFancyConsole();
```

`AddFancyConsole()` binds options from `Logging:FancyConsole`. The delegate overload applies code
configuration on top of bound configuration:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.FancyConsole.Configuration;

builder.Logging.AddFancyConsole(options =>
{
    options.Layout = FancyConsoleLayout.SingleLine;
    options.IncludeScopes = true;
    options.UseShortCategoryName = true;
    options.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
    options.LogToStandardErrorThreshold = LogLevel.Error;
});
```

Calling `AddFancyConsole` more than once registers the provider only once.

## Layouts

Choose a layout with `Layout`. Level codes are `TRC`, `DBG`, `INF`, `WRN`, `ERR`, and `CRT`. In Standard,
Minimal, and Detailed, multi-line messages are indented to the message column; SingleLine replaces line breaks with spaces.

### Standard (default)

A header line with the date, UTC offset, and category, followed by the time, level, and message, and a
blank line between entries:

```text
2026-06-11 +10:00 MyApp.Services.OrderService
09:41:23.123  INF Order 42 submitted
                  for customer 7

```

### SingleLine

One line per entry, suited to dense output and `grep`:

```text
09:41:23.123 INF MyApp.Services.OrderService: Order 42 submitted
```

### Minimal

Only the level and message:

```text
INF Order 42 submitted
```

### Detailed

Every available field, labeled, with a blank line between entries. `Event` is shown only for a non-default
event ID, and `Scopes` only when scopes are active. Detailed always includes scopes, regardless of
`IncludeScopes`.

```text
2026-06-11 09:41:23.123 +10:00 Information
  Category:  MyApp.Services.OrderService
  Event:     1001 (OrderSubmitted)
  Thread:    16
  Scopes:    Request 0HN4 => Order 42
  Message:   Order 42 submitted

```

## Options

| Option | Default | Description |
|--------|---------|-------------|
| `Layout` | `Standard` | `Standard`, `SingleLine`, `Minimal`, or `Detailed`. |
| `TimestampFormat` | `null` | .NET date/time format string (invariant culture). `null`/empty uses the layout default: `HH:mm:ss.fff` (Standard, SingleLine) or `yyyy-MM-dd HH:mm:ss.fff zzz` (Detailed). An invalid format falls back to the layout default. Ignored by Minimal; the Standard header always shows the date and offset. |
| `UseUtcTimestamp` | `false` | Show timestamps in UTC instead of local time. |
| `IncludeScopes` | `false` | Append active scopes after the category as ` => scope1 => scope2` (Standard and SingleLine). |
| `UseShortCategoryName` | `false` | Shorten categories to the text after their last `.`. |
| `UseColors` | `true` | Apply level and exception styles. When `false`, all output is unstyled. |
| `LevelStyles` | empty | Per-level Spectre.Console style strings, e.g. `"bold red"`, `"#ff8800"`, `"black on yellow"`. |
| `ExceptionFormat` | `Full` | `Full`, `Pretty`, `Summary`, or `Hidden`. |
| `ExceptionTextStyle` | `"red1"` | Style for `Full` and `Summary` exception text. |
| `LogToStandardErrorThreshold` | `None` | Entries at or above this level go to standard error. `None` writes everything to standard output. |

The same configuration in `appsettings.json`:

```json
{
  "Logging": {
    "FancyConsole": {
      "Layout": "SingleLine",
      "TimestampFormat": "HH:mm:ss",
      "UseUtcTimestamp": false,
      "IncludeScopes": true,
      "UseShortCategoryName": true,
      "UseColors": true,
      "LevelStyles": {
        "Information": "white",
        "Error": "bold red"
      },
      "ExceptionFormat": "Pretty",
      "ExceptionTextStyle": "red1",
      "LogToStandardErrorThreshold": "Error"
    }
  }
}
```

Configuration changes are picked up at runtime and apply to subsequent entries.

### Level styles

Missing levels use the built-in defaults: `grey` (Trace), `seagreen3` (Debug), `skyblue1` (Information),
`gold1` (Warning), `darkorange` (Error), and `fuchsia` (Critical). A mapped `null` or whitespace value
disables styling for that level, and a value that is not a valid style falls back to the default.

Colors are emitted only when the console supports them. Spectre.Console detects terminal capabilities and
honors the `NO_COLOR` environment variable; set `UseColors` to `false` to disable styling unconditionally.

### Scopes

Message-template scopes (`logger.BeginScope("Order {OrderId}", 42)`) are shown as their formatted text,
other key/value scopes as comma-separated `Key=Value` pairs, and any other object via `ToString()`. Null
and empty scopes are skipped. Minimal never shows scopes.

```text
09:41:23.123 INF OrderService => Request 0HN4 => Order 42: Order 42 submitted
```

## Exceptions

| Format | Output |
|--------|--------|
| `Full` | `Exception.ToString()`, indented to the message column and styled with `ExceptionTextStyle`. |
| `Pretty` | Spectre.Console's formatted exception with highlighted types, methods, and paths. |
| `Summary` | Type and message of the exception and each inner exception, e.g. `InvalidOperationException: Boom ---> ArgumentException: Inner`. |
| `Hidden` | Nothing; only the log message is written. |

In Standard layout exceptions follow the message on their own lines, and in Detailed they appear under an
`Exception:` label. In SingleLine and Minimal, `Summary` is appended to the line as ` | {summary}` so each
entry stays on one line; other formats are written on the following lines.

`Pretty` falls back to `Full` when Spectre.Console cannot render the exception, for example an exception
that was never thrown on .NET Framework.

## Output, filtering, and wrapping

The provider does no level filtering of its own. Use standard logging rules, scoped to the `FancyConsole`
provider alias when needed:

```json
{
  "Logging": {
    "FancyConsole": {
      "LogLevel": {
        "Default": "Information",
        "Microsoft": "Warning"
      }
    }
  }
}
```

Each entry is rendered and written as a single unit, so concurrent entries are not interleaved. Empty
messages and `LogLevel.None` entries are skipped. Rendering failures are reported to debug output and
standard error and never thrown into the application.

Spectre.Console wraps long lines at the console width, which is 80 columns when output is redirected.
Use the Microsoft console logger or the file logger when you need unwrapped, machine-parseable output.
