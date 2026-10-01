# Invex Logging Extensions

Useful utilities for [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging).

## Packages

| Package | What it does |
|---|---|
| [`Invex.Extensions.Logging.FancyConsole`](https://www.nuget.org/packages/Invex.Extensions.Logging.FancyConsole) | A colorful, readable console logger built on [Spectre.Console](https://spectreconsole.net/), with multiple layouts, per-level styles, scopes, pretty exceptions, and standard-error routing. |
| [`Invex.Extensions.Logging.File`](https://www.nuget.org/packages/Invex.Extensions.Logging.File) | A dependency-light file logger with size- and time-based rollover, retention limits, routing by scoped group and log level, and buffered or synchronous writing. |

Both packages are standard logging providers: they plug into any ASP.NET Core, Generic Host, or manually
created `ILoggerFactory`, bind options from the `Logging` configuration section, pick up configuration
changes at runtime, and leave level filtering to the standard logging rules. They target `net10.0`,
`net9.0`, `net8.0`, and `netstandard2.0`, and can be used independently or together.

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
| `SingleLine` | One line per entry: time, level, category, and message. |
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
| `IncludeScopes` | `false` | Append active scopes after the category (Standard and SingleLine; Detailed always shows them). |
| `UseShortCategoryName` | `false` | Shorten categories to the text after their last `.`. |
| `UseColors` | `true` | Apply styles when the console supports them; `NO_COLOR` is honored. |
| `LevelStyles` | empty | Per-level Spectre.Console styles, such as `"bold red"` or `"black on yellow"`. |
| `ExceptionFormat` | `Full` | `Full`, `Pretty`, `Summary`, or `Hidden`. |
| `ExceptionTextStyle` | `"red1"` | Style for `Full` and `Summary` exception text. |
| `LogToStandardErrorThreshold` | `None` | Entries at or above this level go to standard error; `None` writes everything to standard output. |

Each entry is written as a single unit, so concurrent entries do not interleave, and rendering failures
are never thrown into the application. See the [fancy console guide](docs/fancy-console.md) for every
layout, style, and exception option.

## File logger

`AddFile` writes plain, parseable log lines to rolling files:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
```

By default it writes to a `Logs` directory relative to the current working directory. The active file is
named after `AppDomain.CurrentDomain.FriendlyName`, rolls over daily or at 100 MiB (whichever happens
first), and retains up to 10 GiB of rolled-over files.

> [!IMPORTANT]
> Buffered writing is the default. Dispose the host or `ILoggerFactory` during graceful shutdown so
> queued entries are flushed. Entries still in memory can be lost if the process crashes or is killed.

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
| `LogName` | `null` | Active base name without `.log`; `null` uses the application domain friendly name. |
| `PerGroupLogName` | empty | Suffixes for selected scoped groups, appended to the base name before any level suffix. A mapped `null` or empty string adds nothing. |
| `PerLevelLogName` | empty | Suffixes for selected levels, appended after the base name and any group suffix. A mapped `null` or empty string adds nothing. |
| `FileSizeLimitBytes` | 100 MiB | Rolls over before a write that would make the active file reach this size. |
| `RolloverInterval` | `Day` | Elapsed interval from file creation: `Infinite`, `Year` (365 days), `Month` (30 days), `Day`, `Hour`, or `Minute`. |
| `MaxTotalSizeBytes` | 10 GiB | Maximum total size of rolled-over files for each base name. The oldest rolled-over file is removed when the limit is reached. |

> [!IMPORTANT]
> `PerLevelLogName` values now append to `LogName` as suffixes instead of replacing it. Update existing
> full-name mappings to suffixes; for example, use `"errors"` with `LogName = "my-app"` to write
> `my-app_errors.log`. See [migration guidance](docs/configuration.md#migrating-existing-level-mappings).

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
never deletes the active file. See [file rollover and retention](docs/rollover-and-retention.md).

Buffered mode queues entries for a dedicated background thread that writes batches of up to 10 entries.
Use direct mode when an entry must be written before the log call returns:

```csharp
builder.Logging.AddFile(buffered: false);
```

Both modes retry failed writes up to five times, report failures to console/debug output, and drop entries
that still cannot be written. See [buffered versus direct writing](docs/buffering.md) and the
[log format guide](docs/log-format.md).

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
- Fancy console logger: [guide](docs/fancy-console.md)
- File logger: [configuration](docs/configuration.md), [rollover and retention](docs/rollover-and-retention.md),
  [buffered versus direct writing](docs/buffering.md), and [log output format](docs/log-format.md)
- [API reference](api/index.md)

## License

Licensed under the terms of [LICENSE.txt](LICENSE.txt).
