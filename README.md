# Invex Logging Extensions

Useful utilities for [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging).

## Package

[`Invex.Extensions.Logging.File`](https://www.nuget.org/packages/Invex.Extensions.Logging.File) is a
dependency-light file logger provider with:

- size- and elapsed-time-based rollover;
- per-log-name retention limits;
- optional routing by log level;
- buffered or synchronous writing; and
- runtime configuration reload through the standard options pipeline.

The package targets `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`.

## Quick start

Install the package:

```shell
dotnet add package Invex.Extensions.Logging.File
```

Register the provider with an ASP.NET Core or Generic Host application:

```csharp
using Invex.Extensions.Logging.File;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddFile();

var app = builder.Build();
```

It can also be used with a manually created logger factory:

```csharp
using Invex.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(logging => logging.AddFile());
var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Hello from the file logger!");
```

The default configuration writes to a `Logs` directory relative to the current working directory. The
active file is named after `AppDomain.CurrentDomain.FriendlyName`, rolls over daily or at 100 MiB
(whichever happens first), and retains up to 10 GiB of rolled-over files.

> [!IMPORTANT]
> Buffered writing is the default. Dispose the host or `ILoggerFactory` during graceful shutdown so
> queued entries are flushed. Entries still in memory can be lost if the process crashes or is killed.

## Configuration

The provider uses the alias `File`, so its settings belong under `Logging:File`:

```json
{
  "Logging": {
    "File": {
      "LogDirectory": "Logs",
      "LogName": "my-app",
      "FileSizeLimitBytes": 104857600,
      "RolloverInterval": "Day",
      "MaxTotalSizeBytes": 10737418240,
      "PerLevelLogName": {
        "Error": "my-app-errors",
        "Critical": "my-app-errors"
      }
    }
  }
}
```

The same settings can be supplied in code. Values configured by the delegate are applied after values
bound from `Logging:File`:

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
    options.PerLevelLogName[LogLevel.Error] = "my-app-errors";
});
```

| Option | Default | Description |
|---|---:|---|
| `LogDirectory` | `"Logs"` | Absolute directory, or a directory relative to the current working directory. Created when needed. |
| `LogName` | `null` | Active base name without `.log`; `null` uses the application domain friendly name. |
| `PerLevelLogName` | empty | Alternative base names for selected levels. A mapped `null` also uses the application domain friendly name. |
| `FileSizeLimitBytes` | 100 MiB | Rolls over before a write that would make the active file reach this size. |
| `RolloverInterval` | `Day` | Elapsed interval from file creation: `Infinite`, `Year` (365 days), `Month` (30 days), `Day`, `Hour`, or `Minute`. |
| `MaxTotalSizeBytes` | 10 GiB | Maximum total size of rolled-over files for each base name. The oldest rolled-over file is removed when the limit is reached. |

Configuration changes from reloadable sources are picked up for subsequent writes without restarting.
See the [configuration guide](docs/configuration.md) for the complete reference.

## Files, rollover, and retention

The active file is `{LogName}.log`. When rollover occurs, it is renamed to
`{LogName}_{yyMMdd-HHmmss}.log`; collisions receive `_1`, `_2`, and later suffixes. Time rollover uses
elapsed durations, not calendar boundaries, and checks occur only when an entry is written.

Retention is evaluated independently for each base name and never deletes the active file. Since one
rolled-over file is deleted per rollover, an existing directory may take several rollovers to converge
after the retention limit is lowered. See [file rollover and retention](docs/rollover-and-retention.md).

## Buffered versus direct writing

Buffered mode is the default. Log calls enqueue formatted entries on an unbounded in-memory queue, and a
dedicated background thread writes batches of up to 10 entries. This keeps file I/O off application
threads, but queued entries may be lost on abrupt process termination.

Use direct mode when the entry must be written before the log call returns:

```csharp
builder.Logging.AddFile(buffered: false);
```

Direct mode performs rollover, retention, and file I/O synchronously on the calling thread. Both modes
retry failed writes up to five times, report failures to console/debug output, and drop entries that
still cannot be written. See [buffered versus direct writing](docs/buffering.md).

## Filtering and output format

The provider does not apply log-level filtering itself. Use standard logging rules, scoped to the `File`
provider alias when needed:

```json
{
  "Logging": {
    "File": {
      "LogLevel": {
        "Default": "Warning",
        "MyApp.Services": "Information"
      }
    }
  }
}
```

Entries use the format:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
```

Structured message placeholders are rendered by `Microsoft.Extensions.Logging`; scopes are not included,
and empty formatted messages are skipped. See the [log format guide](docs/log-format.md).

## Documentation

- [Getting started](docs/getting-started.md)
- [Configuration](docs/configuration.md)
- [File rollover and retention](docs/rollover-and-retention.md)
- [Buffered versus direct writing](docs/buffering.md)
- [Log output format](docs/log-format.md)
- [API reference](api/index.md)

## License

Licensed under the terms of [LICENSE.txt](LICENSE.txt).
