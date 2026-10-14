# Invex.Extensions.Logging

[![Build](https://github.com/Invex-Games/logging-extensions/actions/workflows/Build.yml/badge.svg)](https://github.com/Invex-Games/logging-extensions/actions/workflows/Build.yml)
[![NuGet](https://img.shields.io/nuget/v/Invex.Extensions.Logging.FancyConsole.svg)](https://www.nuget.org/packages/Invex.Extensions.Logging.FancyConsole)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/Invex-Games/logging-extensions/blob/main/LICENSE.txt)
[![.NET](https://img.shields.io/badge/.NET-10.0%20%7C%20Standard%202.0-512BD4.svg)](https://dotnet.microsoft.com/)

Useful utilities for [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging):
a readable, styled console logger, a rolling file logger, and helpers for logging before the host
starts. The providers plug into the standard logging pipeline, so they work with ASP.NET Core, the
Generic Host, or a manually created `ILoggerFactory`.

## Packages

| Package | What it does |
|---|---|
| [`Invex.Extensions.Logging.FancyConsole`](https://www.nuget.org/packages/Invex.Extensions.Logging.FancyConsole) | A colorful, readable console logger built on [Spectre.Console](https://spectreconsole.net/), with multiple layouts, per-level styles, scopes, pretty exceptions, and standard-error routing. |
| [`Invex.Extensions.Logging.File`](https://www.nuget.org/packages/Invex.Extensions.Logging.File) | A dependency-light file logger with size- and time-based rollover, retention limits, routing by scoped group and log level, and buffered or synchronous writing. |
| [`Invex.Extensions.Logging.Utils`](https://www.nuget.org/packages/Invex.Extensions.Logging.Utils) | Helpers for creating a logger before a host is built, owning its factory, and logging startup information. |

The providers can be used independently or together; Utils creates a separate factory with the
providers you choose.

## Features

- **Four console layouts.** `Standard`, `SingleLine`, `Minimal`, and `Detailed`, with per-level Spectre.Console styles.
- **Readable exceptions.** Render exceptions in `Full`, `Pretty`, one-line `Summary`, or `Hidden` form.
- **Standard-error routing.** Send entries at or above a chosen level to standard error.
- **Rolling log files.** Roll over by size and elapsed time, and purge old archives per resolved file name.
- **Routing by group and level.** Send scoped groups and selected levels to their own files with `BeginGroupScope` and suffix mappings.
- **Buffered or direct writing.** Queue entries for a background writer, or write on the calling thread.
- **Standard options pipeline.** Bind from `Logging:FancyConsole` and `Logging:File`, reload at runtime, and filter with the standard logging rules.
- **Startup logging.** Log before the host is built and emit a consistent startup message with `LogUtil`.

## Installation

```shell
dotnet add package Invex.Extensions.Logging.FancyConsole
dotnet add package Invex.Extensions.Logging.File
dotnet add package Invex.Extensions.Logging.Utils
```

Install only the packages you need. All three target `net10.0` and
`netstandard2.0`. FancyConsole depends on Spectre.Console; File depends on System.IO.Abstractions;
both build on `Microsoft.Extensions.Logging.Configuration`. Utils depends on
`Microsoft.Extensions.Logging` and `Microsoft.Extensions.Hosting.Abstractions`.

## Quick start

Register the providers with your host. A common setup is readable console output while you watch the
app, plus rolling files for later inspection:

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

## Usage

### Style console output

Pick a layout, styles, and an exception format in code or under `Logging:FancyConsole`. Values set by
the delegate are applied after values bound from configuration:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.FancyConsole.Configuration;
using Microsoft.Extensions.Logging;

builder.Logging.AddFancyConsole(options =>
{
    options.Layout = FancyConsoleLayout.SingleLine;
    options.UseShortCategoryName = true;
    options.LevelStyles[LogLevel.Error] = "bold red";
    options.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
    options.LogToStandardErrorThreshold = LogLevel.Error;
});
```

```text
09:41:23.123 INF OrderService: Order 42 submitted
```

See [Fancy Console Logger](https://github.com/Invex-Games/logging-extensions/blob/main/docs/fancy-console.md)
for every layout, option, style, and exception format.

### Write rolling log files

`AddFile` writes plain, parseable lines to `Logs/{AppDomain.CurrentDomain.FriendlyName}.log`. Before a
write, it rolls the file over when it is at least 24 hours old or the write would bring it to at least
100 MiB:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
```

```csharp
using Invex.Extensions.Logging.File;
using Invex.Extensions.Logging.File.Configuration;

builder.Logging.AddFile(options =>
{
    options.LogName = "my-app";
    options.FileSizeLimitBytes = 50L * 1024 * 1024;
    options.RolloverInterval = FileRolloverInterval.Hour;
});
```

> [!IMPORTANT]
> Buffered writing is the default. Dispose the host or `ILoggerFactory` during graceful shutdown so
> queued entries are flushed; entries still in memory can be lost if the process crashes or is killed.

See [File Logger Configuration](https://github.com/Invex-Games/logging-extensions/blob/main/docs/configuration.md),
[File Rollover and Retention](https://github.com/Invex-Games/logging-extensions/blob/main/docs/rollover-and-retention.md),
and [Buffered vs. Direct Writing](https://github.com/Invex-Games/logging-extensions/blob/main/docs/buffering.md).

### Route entries by group and level

Map groups and levels to file name suffixes, then set the group with a scope:

```csharp
builder.Logging.AddFile(options =>
{
    options.LogName = "my-app";
    options.PerGroupLogName["Orders"] = "orders";
    options.PerLevelLogName[LogLevel.Error] = "errors";
});

using (logger.BeginGroupScope("Orders"))
{
    logger.LogInformation("Order submitted"); // my-app_orders.log
    logger.LogError("Payment failed");       // my-app_orders_errors.log
}
```

> [!IMPORTANT]
> `PerLevelLogName` values append to `LogName` as suffixes instead of replacing it, `LogName` defaults
> directly to the application friendly name, and `DefaultLogName` has been removed. See the
> [migration guidance](https://github.com/Invex-Games/logging-extensions/blob/main/docs/configuration.md#migrating-existing-level-mappings).

See [routing groups to separate files](https://github.com/Invex-Games/logging-extensions/blob/main/docs/configuration.md#routing-groups-to-separate-files)
for the complete rules.

### Log before the host starts

`LogUtil.CreateHostLogger` creates a logger with category `Host` and its own factory, filtered to
`Warning` for `Microsoft` categories. Register the providers you need in the callback:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.Utils;
using Microsoft.Extensions.Logging;

using var startupLogger = LogUtil.CreateHostLogger(logging => logging.AddFancyConsole());
startupLogger.LogStartupInfo<Program>();
startupLogger.LogInformation("Configuring services...");
```

See [Logging Utilities](https://github.com/Invex-Games/logging-extensions/blob/main/docs/logging-utilities.md)
for factory ownership and the startup message fields.

### Filter levels per provider

Neither provider filters levels itself. Use the standard
[log filtering rules](https://learn.microsoft.com/dotnet/core/extensions/logging#configure-logging),
scoped to the `FancyConsole` or `File` provider alias when needed:

```json
{
  "Logging": {
    "LogLevel": { "Default": "Information" },
    "File": { "LogLevel": { "Default": "Warning" } }
  }
}
```

See [Getting Started](https://github.com/Invex-Games/logging-extensions/blob/main/docs/getting-started.md#filtering-log-levels).

## Documentation

Full documentation: [https://invex-games.github.io/logging-extensions/](https://invex-games.github.io/logging-extensions/)

| Guide | Contents |
|---|---|
| [Introduction](https://github.com/Invex-Games/logging-extensions/blob/main/docs/introduction.md) | What the packages are for, key concepts, and design principles |
| [Getting Started](https://github.com/Invex-Games/logging-extensions/blob/main/docs/getting-started.md) | Installation, registration, defaults, and level filtering |
| [Fancy Console Logger](https://github.com/Invex-Games/logging-extensions/blob/main/docs/fancy-console.md) | Layouts, options, level styles, scopes, and exceptions |
| [File Logger Configuration](https://github.com/Invex-Games/logging-extensions/blob/main/docs/configuration.md) | Every file option, group and level routing, and migration notes |
| [File Rollover and Retention](https://github.com/Invex-Games/logging-extensions/blob/main/docs/rollover-and-retention.md) | File naming, size and time rollover, and archive purging |
| [Buffered vs. Direct Writing](https://github.com/Invex-Games/logging-extensions/blob/main/docs/buffering.md) | Write modes, shutdown, and error handling |
| [File Log Output Format](https://github.com/Invex-Games/logging-extensions/blob/main/docs/log-format.md) | The shape of each log line and parsing tips |
| [Logging Utilities](https://github.com/Invex-Games/logging-extensions/blob/main/docs/logging-utilities.md) | Startup logging, factory ownership, and startup information |
| [API Reference](https://invex-games.github.io/logging-extensions/api/index.html) | Generated reference for every public type |

## Development

Use the .NET SDK selected by `global.json` and run from the repository root:

```shell
dotnet build Invex.Extensions.Logging.slnx
dotnet test Invex.Extensions.Logging.slnx
```

The documentation site is built with [DocFX](https://dotnet.github.io/docfx/) from the pages in
`docs/` and the XML doc comments in `src/`. DocFX reads the API from the compiled Release assemblies,
because its source-based extraction omits methods declared in C# 14 `extension` blocks such as
`AddFancyConsole` and `AddFile`. The Atom targets build the libraries in Release before running DocFX:

```shell
dotnet run --project _atom -- BuildDocs
dotnet run --project _atom -- ServeDocs
```

(`atom BuildDocs` and `atom ServeDocs` when the Atom tool is installed.) To run DocFX directly, build
Release first:

```shell
dotnet build Invex.Extensions.Logging.slnx --configuration Release
docfx docfx.json --warningsAsErrors
```

The site is generated in `_site/`; add `--serve` to preview it locally. Files under `api/` with a
`.yml` extension are generated; edit the source XML comments or the Markdown pages instead.

## License

Licensed under the [MIT License](https://github.com/Invex-Games/logging-extensions/blob/main/LICENSE.txt).
