# Getting Started

`Invex.Extensions.Logging.File` is a file logger provider for `Microsoft.Extensions.Logging`. It plugs into
the standard logging pipeline alongside the console, debug, and other providers, and writes formatted log
entries to rolling files on disk.

## Installation

```shell
dotnet add package Invex.Extensions.Logging.File
```

The package targets `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`, so it can be used from modern .NET,
older .NET (Core) versions, and .NET Framework applications.

## Registering the provider

### ASP.NET Core / Generic Host

```csharp
using Invex.Extensions.Logging.File;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFile();

var app = builder.Build();
```

### Console applications (manual `LoggerFactory`)

```csharp
using Invex.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddFile();
});

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Hello from the file logger!");
```

> [!IMPORTANT]
> In buffered mode (the default), entries are written by a background thread. Make sure the logger factory /
> host is disposed on shutdown so any pending entries are flushed to disk. Hosted apps do this automatically;
> with a manual `LoggerFactory`, the `using` statement above handles it.

## What you get out of the box

With no configuration at all, the defaults are:

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

## Filtering log levels

The file provider does not filter levels itself — it relies on the standard
[log filtering rules](https://learn.microsoft.com/dotnet/core/extensions/logging#configure-logging). Use the
provider alias `File` to scope rules to this provider:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
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

- [Configuration](configuration.md) — every option, with defaults and examples.
- [File rollover and retention](rollover-and-retention.md) — how files are named, rolled, and purged.
- [Buffered vs. direct writing](buffering.md) — choosing the right write mode.
- [Log output format](log-format.md) — the exact shape of each log line.

