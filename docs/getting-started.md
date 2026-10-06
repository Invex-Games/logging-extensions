# Getting Started

Invex Logging Extensions provides two logging providers and a set of helpers for `Microsoft.Extensions.Logging`. Each provider plugs into
the standard logging pipeline alongside the built-in providers, and they can be used on their own or
together:

| Package | Provider alias | Use it for |
|---------|----------------|------------|
| `Invex.Extensions.Logging.FancyConsole` | `FancyConsole` | Colorful, readable console output with selectable layouts and pretty exceptions. |
| `Invex.Extensions.Logging.File` | `File` | Plain, parseable log lines in rolling files with retention limits. |
| `Invex.Extensions.Logging.Utils` | None | A separate logger for host startup/shutdown, factory ownership, and startup information. |

All three projects target `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`. The provider test suites
also run on .NET Framework 4.8 using their `netstandard2.0` builds. Building this repository requires
the SDK selected by `global.json` (.NET 10 or a later stable SDK), independently of the framework your app targets.

## Installation

```shell
dotnet add package Invex.Extensions.Logging.FancyConsole
dotnet add package Invex.Extensions.Logging.File
```

Install only the package you need, or both.

For startup logging and a separately owned host logger, also install the utilities package:

```shell
dotnet add package Invex.Extensions.Logging.Utils
```

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

`ClearProviders()` removes all registered providers, including the default console, debug, and event
providers. Use it when replacing the default set. Adding File alone does not require clearing providers;
adding FancyConsole while retaining the default console provider can produce duplicate console output.

For a Generic Host application, the same registration works with
`Host.CreateApplicationBuilder(args)` from `Microsoft.Extensions.Hosting`:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders().AddFancyConsole().AddFile();

using var host = builder.Build();
await host.RunAsync();
```

The host's logging configuration supplies the provider sections in `appsettings.json` and supports
reload when the configuration source does. Configuration delegates passed to `AddFile` or
`AddFancyConsole` run after binding and reapply their overrides whenever the options are recreated.

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

A manual factory does not load `appsettings.json` on its own. To use an existing configuration, call
`AddConfiguration` with its `Logging` section before registering the providers:

```csharp
using Microsoft.Extensions.Configuration;

// configuration is an IConfiguration loaded by your application.
using var loggerFactory = LoggerFactory.Create(logging => logging
    .AddConfiguration(configuration.GetSection("Logging"))
    .AddFancyConsole()
    .AddFile());
```

Call `AddFile` with one write mode per factory. Repeated registration of the same mode does not add
another provider, but registering both buffered and direct modes adds two providers and duplicates
file output. See [buffered versus direct writing](buffering.md).

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
| File name       | `{AppDomain.CurrentDomain.FriendlyName}.log`                             |
| Rollover        | Before a write to an existing file aged at least 24 hours, or when the write would bring it to at least 100 MiB |
| Retention       | At most one oldest archive deleted per file creation/rollover when that name's archive total reaches 10 GiB |
| Write mode      | Buffered (background thread)                                            |

A typical `Logs` directory after a few days looks like:

```text
Logs/
    MyApp.log                  <- active file
    MyApp_260609-084512.log    <- rolled over
    MyApp_260610-091304.log    <- rolled over
```

Size limits are rollover and cleanup thresholds, not hard quotas. Buffered writes can contain several
entries, and an entry or batch larger than the file limit is written in full. Active files do not count
toward retention. See [configuration](configuration.md) for options and
[rollover and retention](rollover-and-retention.md) for the exact rules.

## Logging before the host is built

Use `LogUtil.CreateHostLogger` from `Invex.Extensions.Logging.Utils` to create a logger with category
`Host` and its own disposable factory. Register providers in its callback; no provider is added by
default. See [logging utilities](logging-utilities.md) for a complete example and startup information.

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

Utilities:

- [Logging utilities](logging-utilities.md) — startup logging, configuration, and factory lifetime.
