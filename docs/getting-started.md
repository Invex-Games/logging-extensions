# Getting Started

Invex Logging Extensions provides two logging providers for `Microsoft.Extensions.Logging`. Each plugs into
the standard logging pipeline alongside the built-in providers, and they can be used on their own or
together:

| Package | Provider alias | Use it for |
|---------|----------------|------------|
| `Invex.Extensions.Logging.FancyConsole` | `FancyConsole` | Colorful, readable console output with selectable layouts and pretty exceptions. |
| `Invex.Extensions.Logging.File` | `File` | Plain, parseable log lines in rolling files with retention limits. |

Both packages target `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`, so they can be used from modern
.NET, older .NET (Core) versions, and .NET Framework applications.

## Installation

```shell
dotnet add package Invex.Extensions.Logging.FancyConsole
dotnet add package Invex.Extensions.Logging.File
```

Install only the package you need, or both.

## Registering the providers

### ASP.NET Core / Generic Host

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();   // optional: replaces the default console logger
builder.Logging.AddFancyConsole();
builder.Logging.AddFile();

var app = builder.Build();
```

Clearing providers avoids writing every entry twice to the console when the default Microsoft console
logger is also registered.

### Console applications (manual `LoggerFactory`)

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddFancyConsole();
    logging.AddFile();
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Hello, world!");
```

> [!IMPORTANT]
> The file logger buffers entries by default and writes them on a background thread. Make sure the logger
> factory / host is disposed on shutdown so any pending entries are flushed to disk. Hosted apps do this
> automatically; with a manual `LoggerFactory`, the `using` statement above handles it.

## What you get out of the box

### Fancy console logger

With no configuration, `AddFancyConsole()` uses the Standard layout: a header with the date, UTC offset,
and category, followed by the local time, level code, and message:

```text
2026-06-11 +10:00 MyApp.Services.OrderService
09:41:23.123  INF Order 42 submitted

```

| Behavior    | Default                                                                  |
|-------------|--------------------------------------------------------------------------|
| Layout      | `Standard`                                                               |
| Colors      | On, when the console supports them (`NO_COLOR` is honored)               |
| Scopes      | Not shown                                                                |
| Exceptions  | `Full` (`Exception.ToString()`), indented and styled                     |
| Output      | Everything to standard output                                            |

See the [fancy console guide](fancy-console.md) for the other layouts and options.

### File logger

With no configuration, `AddFile()` uses:

| Behavior        | Default                                                                 |
|-----------------|-------------------------------------------------------------------------|
| Log directory   | `Logs`, relative to the current working directory (created if missing) |
| File name       | `{ApplicationName}.log`                                                 |
| Rollover        | Daily, or when the file reaches 100 MiB — whichever comes first         |
| Retention       | Oldest rolled-over file deleted once the total reaches 10 GiB           |
| Write mode      | Buffered (background thread)                                            |

A typical `Logs` directory after a few days looks like:

```text
Logs/
    MyApp.log                  <- active file
    MyApp_260609-084512.log    <- rolled over
    MyApp_260610-091304.log    <- rolled over
```

See [configuration](configuration.md) for the file logger options.

## Filtering log levels

Neither provider filters levels itself — both rely on the standard
[log filtering rules](https://learn.microsoft.com/dotnet/core/extensions/logging#configure-logging). Use the
provider aliases `FancyConsole` and `File` to scope rules to each provider:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    },
    "FancyConsole": {
      "LogLevel": {
        "Default": "Debug",
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

## Next steps

Fancy console logger:

- [Fancy console guide](fancy-console.md) — layouts, options, level styles, scopes, and exceptions.

File logger:

- [Configuration](configuration.md) — every option, with defaults and examples.
- [File rollover and retention](rollover-and-retention.md) — how files are named, rolled, and purged.
- [Buffered vs. direct writing](buffering.md) — choosing the right write mode.
- [Log output format](log-format.md) — the exact shape of each log line.
