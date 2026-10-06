# File Logger Configuration

The file logger is configured via the `FileLoggerConfiguration` options class. Options can be supplied from
configuration (the `Logging:File` section), from code, or both — values set in code override bound values.

## Options reference

| Option               | Type                            | Default        | Description                                                                                                   |
|----------------------|---------------------------------|----------------|---------------------------------------------------------------------------------------------------------------|
| `LogDirectory`       | `string`                        | `"Logs"`       | Directory for log files. Absolute, or relative to the current working directory. Created automatically.        |
| `LogName`            | `string`                        | `AppDomain.CurrentDomain.FriendlyName` | Base file name (no extension), initialized to the application's friendly name. |
| `PerGroupLogName`    | `Dictionary<string, string?>`  | empty          | Group suffixes appended to the base name before any level suffix. Null or empty values add nothing.           |
| `PerLevelLogName`    | `Dictionary<LogLevel, string?>` | empty          | Level suffixes appended after the base name and any group suffix. Null or empty values add nothing.             |
| `FileSizeLimitBytes` | `long`                          | `104857600` (100 MiB) | Rollover threshold for an existing active file plus the pending UTF-8 entry or batch. Equality triggers rollover. |
| `RolloverInterval`   | `FileRolloverInterval`          | `Day`          | Time-based rollover interval: `Infinite`, `Year`, `Month`, `Day`, `Hour`, or `Minute`.                          |
| `MaxTotalSizeBytes`  | `long`                          | `10737418240` (10 GiB) | Archive retention threshold per resolved file name. At most one oldest archive is deleted when the threshold is met. |

Use a positive value for each size setting. Zero or negative values do not disable either feature:
`FileSizeLimitBytes` then rolls an existing file before every write, and `MaxTotalSizeBytes` deletes one
qualifying archive whenever retention is checked. To disable time-based rollover, use `Infinite`.

The size settings are thresholds, not hard quotas. A single entry or buffered batch is never split, and
retention excludes active files. See [rollover and retention](rollover-and-retention.md) for sizing details.

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

Keep file names and suffixes to file name components without `.log` or directory separators. Set the
directory through `LogDirectory`. Names are not trimmed or sanitized, and invalid destination paths
follow the [write retry and drop policy](buffering.md#error-handling). `LogName` is initialized to
`AppDomain.CurrentDomain.FriendlyName` when the options instance is created. An empty string is used
literally and produces `.log` without other suffixes.

### SetLogNameSuffix helper

`SetLogNameSuffix(string suffix)` replaces `LogName` with
`$"{AppDomain.CurrentDomain.FriendlyName}_{suffix}"`, regardless of the currently configured name.
For an application whose friendly name is `MyApp`:

| Call | Resulting `LogName` | Active file before group/level routing |
|---|---|---|
| `options.SetLogNameSuffix("worker")` | `"MyApp_worker"` | `MyApp_worker.log` |
| `options.SetLogNameSuffix("")` | `"MyApp_"` | `MyApp_.log` |

To restore the unsuffixed application friendly name, assign it directly:

```csharp
options.LogName = AppDomain.CurrentDomain.FriendlyName;
```

## Routing levels to separate files

`PerLevelLogName` maps a `LogLevel` to a suffix appended to `LogName`, separated by an underscore.
Without a group suffix, a mapped level writes to `{baseName}_{levelSuffix}.log`; missing, null, or empty
level suffixes leave `{baseName}.log`. The base name defaults to `AppDomain.CurrentDomain.FriendlyName`.
Matching group suffixes are inserted before level suffixes, as described below.

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

With those levels enabled by the framework's filters, produces:

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
Null level mappings now omit the level suffix and retain the configured `LogName`.

## Migrating log names

`LogName` is now a non-nullable `string` initialized to `AppDomain.CurrentDomain.FriendlyName`.
Leave it unset to use that default, or assign the friendly name explicitly to reset it; assigning null
no longer selects the application name. The `DefaultLogName` constant has been removed.

`SetLogNameSuffix` now takes a non-nullable suffix and includes the application's friendly name. For
example, `SetLogNameSuffix("worker")` changes from `_worker.log` to `MyApp_worker.log` for an application
named `MyApp`. It replaces any custom `LogName`; an empty suffix produces `MyApp_.log`.

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

File names always start with `LogName`, which defaults to `AppDomain.CurrentDomain.FriendlyName`.
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
Whitespace-only group names and suffixes count as nonempty and are used literally.

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
group. The formatted message and timestamp are also captured before queueing.

Code configuration delegates run again when the options are recreated, so values they set continue to
override bound values after a reload. Changing a route leaves its previous files in place; retention only
examines archives for the route currently being written. The `buffered` registration flag is not an
option and cannot be changed through `Logging:File` at runtime.

## Default constants

Fixed scalar defaults are exposed as public constants on `FileLoggerConfiguration` for use in your own code:

- `FileLoggerConfiguration.DefaultLogDirectory`
- `FileLoggerConfiguration.DefaultFileSizeLimitBytes`
- `FileLoggerConfiguration.DefaultRollingInterval`
- `FileLoggerConfiguration.DefaultMaxTotalSizeBytes`

`LogName` defaults to `AppDomain.CurrentDomain.FriendlyName` and has no default constant.
`PerGroupLogName` and `PerLevelLogName` each default to an empty dictionary and have no default constant.

