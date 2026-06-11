# Invex Logging Extensions

Useful utilities for [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging).

## Packages

| Package                         | Description                                                                                                                          |
|---------------------------------|--------------------------------------------------------------------------------------------------------------------------------------|
| `Invex.Extensions.Logging.File` | A simple, dependency-light file logger provider with size- and time-based rollover, disk usage caps, and per-level log file routing. |

Supported targets: `net10.0`, `net9.0`, `net8.0`, and `netstandard2.0`.

## Quick start

Install the package:

```shell
dotnet add package Invex.Extensions.Logging.File
```

Register the provider:

```csharp
using Invex.Extensions.Logging.File;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFile();
```

That's it — log entries are now written to a `Logs` directory next to your application, in files named after
the application, rolled over daily or at 100 MiB (whichever comes first), with total disk usage capped at 10 GiB.

Log entries look like this:

```text
[2026-06-11 09:41:23.123 +10:00 INF MyApp.Services.OrderService] Order 42 submitted
```

## Configuration

Configure via `appsettings.json` (the provider alias is `File`):

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
        "Error": "my-app-errors"
      }
    }
  }
}
```

Or in code:

```csharp
builder.Logging.AddFile(options =>
{
    options.LogDirectory = "Logs";
    options.LogName = "my-app";
    options.RolloverInterval = FileRolloverInterval.Hour;
});
```

See the [configuration guide](docs/configuration.md) for every option and its default.

## Buffered vs. direct writing

By default, log entries are queued in memory and written to disk by a dedicated background thread, keeping
file I/O off your application threads. If you need every entry persisted before the log call returns (for
example, in short-lived tools where the process may exit abruptly), use direct mode:

```csharp
builder.Logging.AddFile(buffered: false);
```

See [buffered vs. direct writing](docs/buffering.md) for details and trade-offs.

## Documentation

- [Getting started](docs/getting-started.md)
- [Configuration](docs/configuration.md)
- [File rollover and retention](docs/rollover-and-retention.md)
- [Buffered vs. direct writing](docs/buffering.md)
- [Log output format](docs/log-format.md)
- [API reference](api/index.md)

## License

Licensed under the terms of [LICENSE.txt](LICENSE.txt).


