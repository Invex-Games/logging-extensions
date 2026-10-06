# Invex Logging Extensions

Useful utilities for [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging).

## Packages

| Package | What it does |
|---|---|
| [`Invex.Extensions.Logging.FancyConsole`](https://www.nuget.org/packages/Invex.Extensions.Logging.FancyConsole) | A colorful, readable console logger built on [Spectre.Console](https://spectreconsole.net/), with multiple layouts, per-level styles, scopes, pretty exceptions, and standard-error routing. |
| [`Invex.Extensions.Logging.File`](https://www.nuget.org/packages/Invex.Extensions.Logging.File) | A dependency-light file logger with size- and time-based rollover, retention limits, routing by scoped group and log level, and buffered or synchronous writing. |
| [`Invex.Extensions.Logging.Utils`](https://www.nuget.org/packages/Invex.Extensions.Logging.Utils) | Helpers for creating a logger before a host is built, owning its factory, and logging startup information. |

The FancyConsole and File packages are standard logging providers: they plug into ASP.NET Core, Generic Host, or manually
created `ILoggerFactory`, bind options from the `Logging` configuration section, pick up configuration
changes from sources that support reload, and leave level filtering to the standard logging rules. All
three projects target `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`. The providers can be used
independently or together; Utils creates a separate factory with the providers you choose.

## Quick start

Install one or both packages:

```shell
dotnet add package Invex.Extensions.Logging.FancyConsole
dotnet add package Invex.Extensions.Logging.File
```

Register them with your host. A common setup is readable console output while you watch the app, plus
rolling files for later inspection:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFancyConsole();
builder.Logging.AddFile();

var app = builder.Build();
```

They also work with a manually created logger factory:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(logging => logging
    .AddFancyConsole()
    .AddFile());

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Hello, world!");
```

See [getting started](docs/getting-started.md) for defaults and level filtering.

## Fancy console logger

`AddFancyConsole` writes styled, human-friendly output to the console. Pick from four layouts:

| Layout | Output |
|---|---|
| `Standard` (default) | A date and category header, then time, level, and message, with a blank line between entries. |
| `SingleLine` | Time, level, category, and message; message line breaks become spaces. Full and Pretty exceptions follow on separate lines. |
| `Minimal` | Only the level and message. |
| `Detailed` | Every available field (category, event, thread, scopes, and message), labeled on separate lines. |

Standard:

```text
2026-06-11 +10:00 MyApp.Services.OrderService
09:41:23.123  INF Order 42 submitted

```

SingleLine:

```text
09:41:23.123 INF MyApp.Services.OrderService: Order 42 submitted
```

Exceptions can be rendered in `Full`, Spectre.Console `Pretty`, one-line `Summary`, or `Hidden` form, and
entries at or above a chosen level can be routed to standard error. Configure in code:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.FancyConsole.Configuration;
using Microsoft.Extensions.Logging;

builder.Logging.AddFancyConsole(options =>
{
    options.Layout = FancyConsoleLayout.SingleLine;
    options.IncludeScopes = true;
    options.UseShortCategoryName = true;
    options.LevelStyles[LogLevel.Error] = "bold red";
    options.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
    options.LogToStandardErrorThreshold = LogLevel.Error;
});
```

Or under `Logging:FancyConsole` (provider alias `FancyConsole`). Values configured by the delegate are
applied after values bound from configuration:

```json
{
  "Logging": {
    "FancyConsole": {
      "Layout": "SingleLine",
      "IncludeScopes": true,
      "LevelStyles": {
        "Error": "bold red"
      },
      "ExceptionFormat": "Pretty",
      "LogToStandardErrorThreshold": "Error"
    }
  }
}
```

| Option | Default | Description |
|---|---:|---|
| `Layout` | `Standard` | `Standard`, `SingleLine`, `Minimal`, or `Detailed`. |
| `TimestampFormat` | `null` | .NET date/time format string; `null` or empty uses the layout default. |
| `UseUtcTimestamp` | `false` | Show timestamps in UTC instead of local time. |
| `IncludeScopes` | `false` | Append active scopes after the category in Standard and SingleLine; Detailed always shows them, and Minimal omits them. |
| `UseShortCategoryName` | `false` | Shorten categories to the text after their last `.`. |
| `UseColors` | `true` | Apply styles when the console supports them; `NO_COLOR` is honored. |
| `LevelStyles` | empty | Per-level Spectre.Console styles, such as `"bold red"` or `"black on yellow"`. |
| `ExceptionFormat` | `Full` | `Full`, `Pretty`, `Summary`, or `Hidden`. |
| `ExceptionTextStyle` | `"red1"` | Style for `Full` and `Summary` exception text. |
| `LogToStandardErrorThreshold` | `None` | Entries at or above this level go to standard error; `None` writes everything to standard output. |

Each entry is written as a single unit, so concurrent FancyConsole entries do not interleave, and rendering failures
are never thrown into the application. See the [fancy console guide](docs/fancy-console.md) for every
layout, style, and exception option.

## File logger

`AddFile` writes plain, parseable log lines to rolling files:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
```

File output contains the formatted message. Attached exceptions and event IDs are not appended
automatically; include any required details in the message. Messages can span multiple lines. See
[log output format](docs/log-format.md) for examples and parsing guidance.

By default it writes to a `Logs` directory relative to the current working directory. The active file is
named after `AppDomain.CurrentDomain.FriendlyName`, and rolls over before a write when the existing file
is at least 24 hours old or the write would bring it to at least 100 MiB. Retention checks a 10 GiB archive
threshold per resolved file name and removes at most one oldest archive when a file is created or rolled.
These thresholds are not hard disk quotas; see the [rollover guide](docs/rollover-and-retention.md).

> [!IMPORTANT]
> Buffered writing is the default. Dispose the host or `ILoggerFactory` during graceful shutdown so
> queued entries are flushed, even when disposal immediately follows the last log call; no delay is needed.
> Entries still in memory can be lost if the process crashes or is killed.

Configure in code:

```csharp
using Invex.Extensions.Logging.File;
using Invex.Extensions.Logging.File.Configuration;
using Microsoft.Extensions.Logging;

builder.Logging.AddFile(options =>
{
    options.LogDirectory = "Logs";
    options.LogName = "my-app";
    options.FileSizeLimitBytes = 50L * 1024 * 1024;
    options.RolloverInterval = FileRolloverInterval.Hour;
    options.MaxTotalSizeBytes = 1L * 1024 * 1024 * 1024;
    options.PerGroupLogName["Orders"] = "orders";
    options.PerLevelLogName[LogLevel.Error] = "errors";
});
```

Or under `Logging:File` (provider alias `File`). Values configured by the delegate are applied after
values bound from configuration:

```json
{
  "Logging": {
    "File": {
      "LogDirectory": "Logs",
      "LogName": "my-app",
      "FileSizeLimitBytes": 104857600,
      "RolloverInterval": "Day",
      "MaxTotalSizeBytes": 10737418240,
      "PerGroupLogName": {
        "Orders": "orders"
      },
      "PerLevelLogName": {
        "Error": "errors",
        "Critical": "errors"
      }
    }
  }
}
```

| Option | Default | Description |
|---|---:|---|
| `LogDirectory` | `"Logs"` | Absolute directory, or a directory relative to the current working directory. Created when needed. |
| `LogName` | `AppDomain.CurrentDomain.FriendlyName` | Active base name without `.log`, initialized to the application domain friendly name. |
| `PerGroupLogName` | empty | Suffixes for selected scoped groups, appended to the base name before any level suffix. A mapped `null` or empty string adds nothing. |
| `PerLevelLogName` | empty | Suffixes for selected levels, appended after the base name and any group suffix. A mapped `null` or empty string adds nothing. |
| `FileSizeLimitBytes` | 100 MiB | Rolls an existing file before a write that would make it reach this size; an oversized entry or batch is still written in full. |
| `RolloverInterval` | `Day` | Elapsed interval from file creation: `Infinite`, `Year` (365 days), `Month` (30 days), `Day`, `Hour`, or `Minute`. |
| `MaxTotalSizeBytes` | 10 GiB | Archive cleanup threshold per resolved base name. On creation or rollover, removes at most one oldest archive if the total meets the threshold; excludes active files. |

> [!IMPORTANT]
> `PerLevelLogName` values now append to `LogName` as suffixes instead of replacing it. Update existing
> full-name mappings to suffixes; for example, use `"errors"` with `LogName = "my-app"` to write
> `my-app_errors.log`. See [migration guidance](docs/configuration.md#migrating-existing-level-mappings).

`LogName` now defaults directly to the application friendly name, and `SetLogNameSuffix("worker")`
produces `{AppDomain.CurrentDomain.FriendlyName}_worker.log`. `DefaultLogName` has been removed;
see [log name migration guidance](docs/configuration.md#migrating-log-names).

### Routing by group

Use `BeginGroupScope` to set a group for log calls within a scope:

```csharp
using Invex.Extensions.Logging.File;

using (logger.BeginGroupScope("Orders"))
{
    logger.LogInformation("Order submitted"); // my-app_orders.log
    logger.LogError("Payment failed");       // my-app_orders_errors.log
}
```

File names start with `LogName`, followed by the mapped group suffix and then the mapped level suffix,
each separated by an underscore. Missing mappings and null or empty mapped suffixes add no separator or
text. Nested groups override outer groups until disposed, and scopes flow across `await` and logger
categories. Dictionary and formatted scopes such as `logger.BeginScope("{Group}", "Orders")` also work.
Scope values affect routing without appearing in the file's log lines. See
[group routing](docs/configuration.md#routing-groups-to-separate-files) for the complete rules.

### Rollover, retention, and write modes

When rollover occurs, the active `{name}.log` is renamed to `{name}_{yyMMdd-HHmmss}.log`; collisions
receive `_1`, `_2`, and later suffixes. Time rollover uses elapsed durations, not calendar boundaries, and
checks occur only when an entry is written. Retention is evaluated independently for each base name and
never deletes the active file. Case-only path aliases follow the destination filesystem's behavior for
buffered routing, archive naming, and retention. See [file rollover and retention](docs/rollover-and-retention.md).

Buffered mode queues entries for a dedicated background thread that writes batches of up to 10 entries.
Use direct mode to attempt each write on the calling thread before the log call returns:

```csharp
builder.Logging.AddFile(buffered: false);
```

Both modes make an initial write attempt plus up to five retries, report failures to console/debug
output, and drop entries that still cannot be written. A successful write flushes the stream to the
operating system without guaranteeing physical disk durability. See [buffered versus direct writing](docs/buffering.md) and the
[log format guide](docs/log-format.md).

## Logging before the host starts

`Invex.Extensions.Logging.Utils` lets you create a separate logger for startup and shutdown messages.
`CreateHostLogger` uses the category `Host`, adds a `Microsoft` category filter at `Warning`, and runs
your configuration callback. Register the providers you need in that callback:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.Utils;
using Microsoft.Extensions.Logging;

using var startupLogger = LogUtil.CreateHostLogger(logging => logging.AddFancyConsole());
startupLogger.LogStartupInfo<Program>();
startupLogger.LogInformation("Configuring services...");
```

Disposing the returned `HostLogger` disposes its factory and factory-owned providers. It has its own configuration;
the application's later host configuration does not automatically apply to it. See
[logging utilities](docs/logging-utilities.md) for configuration, factory ownership, and startup fields.

## Filtering

Neither provider filters levels itself. Use the standard
[log filtering rules](https://learn.microsoft.com/dotnet/core/extensions/logging#configure-logging),
scoped to a provider alias when needed:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "FancyConsole": {
      "LogLevel": {
        "Microsoft": "Warning"
      }
    },
    "File": {
      "LogLevel": {
        "Default": "Warning",
        "MyApp.Services": "Information"
      }
    }
  }
}
```

## Documentation

- [Getting started](docs/getting-started.md)
- [Logging utilities](docs/logging-utilities.md)
- Fancy console logger: [guide](docs/fancy-console.md)
- File logger: [configuration](docs/configuration.md), [rollover and retention](docs/rollover-and-retention.md),
  [buffered versus direct writing](docs/buffering.md), and [log output format](docs/log-format.md)
- [API reference](api/index.md)

To build the documentation locally, use the SDK selected by `global.json` and run from the repository root:

```shell
dotnet build Invex.Extensions.Logging.slnx --configuration Release
docfx docfx.json --warningsAsErrors
```

DocFX reads the compiled libraries and their XML comments. Build first to include the latest API and
documentation; add `--serve` to preview the generated site in `_site/`.

The Atom `BuildDocs` and `ServeDocs` targets also require the Release assemblies to be built first.

## License

Licensed under the terms of [LICENSE.txt](LICENSE.txt).
