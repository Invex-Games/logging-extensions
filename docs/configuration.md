# File Logger Configuration

The file logger is configured via the `FileLoggerConfiguration` options class. Options can be supplied from
configuration (the `Logging:File` section), from code, or both — values set in code override bound values.

## Options reference

| Option               | Type                            | Default        | Description                                                                                                   |
|----------------------|---------------------------------|----------------|---------------------------------------------------------------------------------------------------------------|
| `LogDirectory`       | `string`                        | `"Logs"`       | Directory for log files. Absolute, or relative to the current working directory. Created automatically.        |
| `LogName`            | `string?`                       | `null`         | Base file name (no extension). `null` uses the application's name (`AppDomain.CurrentDomain.FriendlyName`).     |
| `PerGroupLogName`    | `Dictionary<string, string?>`  | empty          | Group suffixes appended to the base name before any level suffix. Null or empty values add nothing.           |
| `PerLevelLogName`    | `Dictionary<LogLevel, string?>` | empty          | Level suffixes appended after the base name and any group suffix. Null or empty values add nothing.             |
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

> [!NOTE]
> `RolloverInterval` accepts the enum names: `Infinite`, `Year`, `Month`, `Day`, `Hour`, `Minute`.

## Configuring in code

```csharp
using Invex.Extensions.Logging.File;
using Invex.Extensions.Logging.File.Configuration;
using Microsoft.Extensions.Logging;

builder.Logging.AddFile(options =>
{
    options.LogDirectory = "Logs";
    options.LogName = "my-app";
    options.FileSizeLimitBytes = 50L * 1024 * 1024; // 50 MiB
    options.RolloverInterval = FileRolloverInterval.Hour;
    options.MaxTotalSizeBytes = 1L * 1024 * 1024 * 1024; // 1 GiB

    options.PerGroupLogName["Orders"] = "orders";
    options.PerLevelLogName[LogLevel.Error] = "errors";
    options.PerLevelLogName[LogLevel.Critical] = "errors";
});
```

## Routing levels to separate files

`PerLevelLogName` maps a `LogLevel` to a suffix appended to `LogName`, separated by an underscore.
Without a group suffix, a mapped level writes to `{baseName}_{levelSuffix}.log`; missing, null, or empty
level suffixes leave `{baseName}.log`. A null `LogName` uses `AppDomain.CurrentDomain.FriendlyName` as
the base name. Matching group suffixes are inserted before level suffixes, as described below.

```csharp
builder.Logging.AddFile(options =>
{
    options.LogName = "app";

    // Errors and critical entries go to their own file
    options.PerLevelLogName[LogLevel.Error] = "errors";
    options.PerLevelLogName[LogLevel.Critical] = "errors";

    // Trace entries go to a separate diagnostic file
    options.PerLevelLogName[LogLevel.Trace] = "trace";
});
```

Produces:

```text
Logs/
    app.log          <- Debug, Information, Warning
    app_errors.log   <- Error, Critical
    app_trace.log    <- Trace
```

Each file rolls over and is purged independently, using the same `FileSizeLimitBytes`, `RolloverInterval`,
and `MaxTotalSizeBytes` settings.

> [!TIP]
> A `null` or empty value in either mapping dictionary omits that suffix and its separator. The base name
> and any other nonempty suffix still apply.

## Migrating existing level mappings

`PerLevelLogName` now contains suffixes rather than replacement file names. This intentionally changes
the behavior of existing mappings. For example, `LogName = "app"` with an existing `Error = "app-errors"`
mapping now produces `app_app-errors.log`. Change the mapping to `"errors"` to produce `app_errors.log`.
Null level mappings now omit the level suffix instead of selecting the application friendly name;
only a null `LogName` selects that base name.

## Routing groups to separate files

`PerGroupLogName` maps a scoped group to a suffix appended to the base name. Use `BeginGroupScope` to set
the group before logging:

```csharp
using Invex.Extensions.Logging.File;
using Microsoft.Extensions.Logging;

builder.Logging.AddFile(options =>
{
    options.LogName = "app";
    options.PerGroupLogName["Orders"] = "orders";
    options.PerLevelLogName[LogLevel.Error] = "errors";
});

// Later, using an ILogger from the application's logging infrastructure:
using (logger.BeginGroupScope("Orders"))
{
    logger.LogInformation("Order submitted"); // app_orders.log
    logger.LogError("Payment failed");       // app_orders_errors.log
}
```

| Group | Level | File |
|---|---|---|
| `Orders` | `Error` | `app_orders_errors.log` |
| `Orders` | `Information` | `app_orders.log` |
| Missing or unmapped | `Error` | `app_errors.log` |
| Missing or unmapped | `Information` | `app.log` |

File names always start with `LogName`, or `AppDomain.CurrentDomain.FriendlyName` when `LogName` is null.
Append each nonempty mapped suffix in group-then-level order, with an underscore before each suffix:
`{baseName}[_{groupSuffix}][_{levelSuffix}].log`. Brackets denote optional components, not literal text.
Mapped suffixes are used verbatim. A missing, null, or empty mapping adds no text and no separator.
For example, a null group suffix with an `"errors"` level suffix produces `app_errors.log`; two omitted
suffixes produce `app.log`. Routes that resolve to the same name share a file and its rollover and
retention limits.

Case-only destination aliases follow the actual filesystem behavior in the destination directory,
including when batching entries and protecting active files during rollover and retention. See
[file naming](rollover-and-retention.md#file-naming) for the filesystem check used for these aliases.

Groups are case-sensitive by default; a dictionary supplied in code uses its configured comparer. The
scope property key must be exactly `"Group"`.

`BeginGroupScope` delegates to `ILogger.BeginScope` and returns its disposable (which can be `null` if no
provider supports scopes). Standard structured scopes also work:

```csharp
using (logger.BeginScope(new Dictionary<string, object?> { ["Group"] = "Orders" }))
{
    logger.LogInformation("Order submitted");
}

using (logger.BeginScope("{Group}", "Orders"))
{
    logger.LogInformation("Order submitted");
}
```

Scope state must implement `IEnumerable<KeyValuePair<string, object?>>`. The innermost nonempty string
group wins, even when it has no mapping: an unmapped inner group uses normal level/default routing,
rather than an outer group's mapping. Disposing the scope restores the outer group. Null, empty, and
non-string group values, and unstructured scopes, are ignored, leaving any outer group effective.
These rules also apply to values passed through the helper; a null logger throws `ArgumentNullException`.

Scopes flow across `await` and logger categories within the same logging infrastructure; concurrent
execution contexts maintain independent scopes. The helper creates ordinary scope state, which other
logging providers may also inspect. Group data is not included in the file's log-line format.

## Runtime configuration changes

The provider monitors its options with `IOptionsMonitor<T>`. Changes to the `Logging:File` section in
`appsettings.json` (or any reloadable configuration source) are picked up automatically and applied to
subsequent log writes — no restart required.

The group string is captured when `Log` is called. Both writers resolve that group and the entry's level
using configuration at write time (once per batch in buffered mode), so queued entries can use updated
file mappings. Disposing a scope or changing its dictionary after logging does not change the captured
group.

## Default constants

Scalar defaults are exposed as public constants on `FileLoggerConfiguration` for use in your own code:

- `FileLoggerConfiguration.DefaultLogDirectory`
- `FileLoggerConfiguration.DefaultLogName`
- `FileLoggerConfiguration.DefaultFileSizeLimitBytes`
- `FileLoggerConfiguration.DefaultRollingInterval`
- `FileLoggerConfiguration.DefaultMaxTotalSizeBytes`

`PerGroupLogName` and `PerLevelLogName` each default to an empty dictionary and have no default constant.

