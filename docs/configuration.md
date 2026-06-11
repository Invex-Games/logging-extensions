# Configuration

The file logger is configured via the `FileLoggerConfiguration` options class. Options can be supplied from
configuration (the `Logging:File` section), from code, or both — values set in code override bound values.

## Options reference

| Option               | Type                            | Default        | Description                                                                                                   |
|----------------------|---------------------------------|----------------|---------------------------------------------------------------------------------------------------------------|
| `LogDirectory`       | `string`                        | `"Logs"`       | Directory for log files. Absolute, or relative to the current working directory. Created automatically.        |
| `LogName`            | `string?`                       | `null`         | Base file name (no extension). `null` uses the application's name (`AppDomain.CurrentDomain.FriendlyName`).     |
| `PerLevelLogName`    | `Dictionary<LogLevel, string?>` | empty          | Per-level overrides of the file name, routing specific levels to separate files.                               |
| `FileSizeLimitBytes` | `long`                          | `104857600` (100 MiB) | Maximum size of a single log file before it is rolled over.                                              |
| `RolloverInterval`   | `FileRolloverInterval`          | `Day`          | Time-based rollover interval: `Infinite`, `Year`, `Month`, `Day`, `Hour`, or `Minute`.                          |
| `MaxTotalSizeBytes`  | `long`                          | `10737418240` (10 GiB) | Maximum combined size of rolled-over files before the oldest is deleted.                                |

## Configuring via appsettings.json

The provider registers under the alias `File`, so it binds to the `Logging:File` section:

```json
{
  "Logging": {
    "File": {
      "LogDirectory": "C:/logs/my-app",
      "LogName": "my-app",
      "FileSizeLimitBytes": 52428800,
      "RolloverInterval": "Hour",
      "MaxTotalSizeBytes": 1073741824,
      "PerLevelLogName": {
        "Error": "my-app-errors",
        "Critical": "my-app-errors"
      }
    }
  }
}
```

> [!NOTE]
> `RolloverInterval` accepts the enum names: `Infinite`, `Year`, `Month`, `Day`, `Hour`, `Minute`.

## Configuring in code

```csharp
using Invex.Extensions.Logging.File;
using Invex.Extensions.Logging.File.Configuration;

builder.Logging.AddFile(options =>
{
    options.LogDirectory = "Logs";
    options.LogName = "my-app";
    options.FileSizeLimitBytes = 50L * 1024 * 1024; // 50 MiB
    options.RolloverInterval = FileRolloverInterval.Hour;
    options.MaxTotalSizeBytes = 1L * 1024 * 1024 * 1024; // 1 GiB

    options.PerLevelLogName[LogLevel.Error] = "my-app-errors";
    options.PerLevelLogName[LogLevel.Critical] = "my-app-errors";
});
```

## Routing levels to separate files

`PerLevelLogName` maps a `LogLevel` to an alternative base file name. Levels present in the dictionary write
to `{name}.log` instead of the default file; levels not present continue to use `LogName`.

```csharp
builder.Logging.AddFile(options =>
{
    options.LogName = "app";

    // Errors and critical entries go to their own file
    options.PerLevelLogName[LogLevel.Error] = "app-errors";
    options.PerLevelLogName[LogLevel.Critical] = "app-errors";

    // Trace entries go to a separate diagnostic file
    options.PerLevelLogName[LogLevel.Trace] = "app-trace";
});
```

Produces:

```text
Logs/
    app.log          <- Debug, Information, Warning
    app-errors.log   <- Error, Critical
    app-trace.log    <- Trace
```

Each file rolls over and is purged independently, using the same `FileSizeLimitBytes`, `RolloverInterval`,
and `MaxTotalSizeBytes` settings.

> [!TIP]
> A `null` value in `PerLevelLogName` is valid and falls back to the application's name for that level —
> useful if `LogName` is customized but you want certain levels in the default-named file.

## Runtime configuration changes

The provider monitors its options with `IOptionsMonitor<T>`. Changes to the `Logging:File` section in
`appsettings.json` (or any reloadable configuration source) are picked up automatically and applied to
subsequent log writes — no restart required.

## Default constants

All defaults are exposed as public constants on `FileLoggerConfiguration` for use in your own code:

- `FileLoggerConfiguration.DefaultLogDirectory`
- `FileLoggerConfiguration.DefaultLogName`
- `FileLoggerConfiguration.DefaultFileSizeLimitBytes`
- `FileLoggerConfiguration.DefaultRollingInterval`
- `FileLoggerConfiguration.DefaultMaxTotalSizeBytes`

