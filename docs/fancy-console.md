# Fancy Console Logger

`Invex.Extensions.Logging.FancyConsole` is a colorful console logger provider built on
[Spectre.Console](https://spectreconsole.net/). It offers several layouts, per-level styles, optional
scopes, styled or "pretty" exceptions, and standard-error routing, all configurable through the standard
options pipeline with runtime reload.

The library targets `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`.

## Registration

Install the package into your application:

```shell
dotnet add package Invex.Extensions.Logging.FancyConsole
```

Register it with a Generic Host:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFancyConsole();
```

`ClearProviders()` removes the host's default providers, which prevents duplicate console output. Omit
that call when you want to keep other providers, such as the file logger. In an ASP.NET Core app, use
the same logging registration with `WebApplication.CreateBuilder(args)`.

`AddFancyConsole()` binds options from `Logging:FancyConsole`. The delegate overload applies code
configuration on top of bound configuration:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.FancyConsole.Configuration;
using Microsoft.Extensions.Logging;

builder.Logging.AddFancyConsole(options =>
{
    options.Layout = FancyConsoleLayout.SingleLine;
    options.IncludeScopes = true;
    options.UseShortCategoryName = true;
    options.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
    options.LogToStandardErrorThreshold = LogLevel.Error;
});
```

Calling `AddFancyConsole` more than once registers the provider only once. Each delegate you supply is
still applied in registration order, so later delegates can override earlier values.

## Layouts

Choose a layout with `Layout`. Level codes are `TRC`, `DBG`, `INF`, `WRN`, `ERR`, and `CRT`. In Standard,
Minimal, and Detailed, CRLF and LF message line breaks are retained, with continuation lines indented
to the message column. SingleLine replaces CRLF, CR, and LF message line breaks with spaces.

The examples below omit styling and assume the console is wide enough to avoid wrapping.

### Standard (default)

A header line with the date, UTC offset, and category, followed by the time, level, and message, and a
blank line between entries:

```text
2026-06-11 +10:00 MyApp.Services.OrderService
09:41:23.123  INF Order 42 submitted
                  for customer 7

```

### SingleLine

A compact timestamp, level, category, and message:

```text
09:41:23.123 INF MyApp.Services.OrderService: Order 42 submitted
```

Message line breaks are flattened, but line breaks in scopes, categories, and custom timestamp formats
are preserved. Exceptions and terminal wrapping can also add lines; see [Exceptions](#exceptions) and
[Output, filtering, and wrapping](#output-filtering-and-wrapping).

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
| `UseShortCategoryName` | `false` | Shorten categories to the text after their last `.` outside generic arguments. A trailing `.` is retained. Only display changes; category filters still use the full name. |
| `UseColors` | `true` | Apply level and exception styles. When `false`, all output is unstyled. |
| `LevelStyles` | empty | Per-level Spectre.Console style strings for headers, level codes, and Detailed field labels, e.g. `"bold red"`, `"#ff8800"`, `"black on yellow"`. Message text remains unstyled. |
| `ExceptionFormat` | `Full` | `Full`, `Pretty`, `Summary`, or `Hidden`. |
| `ExceptionTextStyle` | `"red1"` | Style for `Full` and `Summary` exception text, also used when `Pretty` falls back to `Full`. Null/empty/whitespace disables styling; an invalid style uses `"red1"`. |
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

Configuration changes apply to subsequent entries when the configuration source emits reload
notifications. The default Generic Host JSON configuration supports this; if you load JSON yourself,
enable `reloadOnChange`. Code configuration delegates run again when options are rebuilt and continue
to override bound values. Each entry uses one captured options instance for timestamping, layout,
scopes, styles, and stream selection.

### Level styles

Missing levels use the built-in defaults: `grey` (Trace), `seagreen3` (Debug), `skyblue1` (Information),
`gold1` (Warning), `darkorange` (Error), and `fuchsia` (Critical). A mapped `null` or whitespace value
disables styling for that level, and a value that is not a valid style falls back to the default.

Colors are emitted according to the output console's capabilities. Spectre.Console detects terminal
capabilities and honors the `NO_COLOR` environment variable; see its
[capabilities reference](https://spectreconsole.net/console/reference/capabilities-reference/).
Set `UseColors` to `false` to disable all styling, including bold text and Pretty exception styling.

Level overrides do not affect exception styles. `Pretty` uses its own highlighting rather than
`ExceptionTextStyle`, unless it falls back to `Full`.

### Scopes

Message-template scopes (`logger.BeginScope("Order {OrderId}", 42)`) are shown as their formatted text,
other key/value scopes as comma-separated `Key=Value` pairs, and any other object via `ToString()`. Null
and empty scope text is skipped; whitespace and line breaks are retained. Scopes appear outermost first
and stop appearing once disposed. Minimal never shows scopes, while Detailed always captures them.

```csharp
using (logger.BeginScope("Request {RequestId}", "0HN4"))
using (logger.BeginScope("Order {OrderId}", 42))
    logger.LogInformation("Order {OrderId} submitted", 42);
```

With `SingleLine`, `IncludeScopes = true`, and `UseShortCategoryName = true`:

```text
09:41:23.123 INF OrderService => Request 0HN4 => Order 42: Order 42 submitted
```

## Exceptions

| Format | Output |
|--------|--------|
| `Full` | `Exception.ToString()`, styled with `ExceptionTextStyle` and indented according to the layout. |
| `Pretty` | Spectre.Console's formatted exception with highlighted types, methods, and paths. |
| `Summary` | Type and message along the `InnerException` chain, e.g. `InvalidOperationException: Boom ---> ArgumentException: Inner`. It does not enumerate every branch of an `AggregateException`. |
| `Hidden` | Nothing; only the log message is written. |

Pass the exception separately so the selected format can apply:

```csharp
logger.LogError(exception, "Failed to submit order {OrderId}", 42);
```

In Standard layout exceptions follow the message on their own lines, and in Detailed they appear under
an `Exception:` label. In SingleLine and Minimal, `Summary` follows the message as ` | {summary}`, with
summary line breaks replaced by spaces. Minimal still retains line breaks in the message itself.
`Full` and `Pretty` appear on following lines, indented by four spaces in both compact layouts.

`Pretty` falls back to `Full` if Spectre.Console cannot construct the exception renderable, for example
some exceptions with unthrown inner exceptions on .NET Framework. `UseColors = false` preserves the
Pretty layout while removing its styling.

## Output, filtering, and wrapping

Messages, categories, scopes, and exception text are treated as literal text. A message containing
`[red]failed[/]` prints those characters; it does not apply Spectre markup. Configure styling through
`LevelStyles` and `ExceptionTextStyle` instead. Message-template values are formatted by the normal
logging pipeline before rendering.

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

`LogToStandardErrorThreshold` controls routing after filtering. For example, `Error` sends Error and
Critical entries to standard error and lower levels to standard output. The whole entry, including
exception output, goes to one stream; attaching an exception does not change routing. `None` sends all
entries to standard output.

Writes are synchronous on the logging thread. A shared lock keeps entries from FancyConsole providers
together even under concurrent logging; unrelated writes to the console can still interleave.
Null/empty formatted messages and `LogLevel.None` entries are skipped, even when an exception is
supplied. Whitespace-only messages are written.

Failures while formatting, capturing scopes, or writing are reported to debug output and, when
possible, standard error without being thrown into the application. Failed writes are not retried,
and any output already written can remain. Internal failure diagnostics go to standard error
regardless of the configured routing threshold.

Spectre.Console wraps long lines at its console profile width, including when output is redirected;
the width comes from the detected console size or Spectre's fallback. Disabling colors does not
disable wrapping. `SingleLine` with `Summary` or `Hidden` reduces extra lines, but it does not guarantee
one physical line per entry. Use a provider designed for structured output when a machine must parse
individual entries, or the [file logger](getting-started.md) to avoid terminal wrapping.
