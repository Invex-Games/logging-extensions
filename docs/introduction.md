# Introduction

Invex.Extensions.Logging is a set of three packages that extend `Microsoft.Extensions.Logging`: two
logging providers (a styled console logger and a rolling file logger) and a small utilities package
for logging outside the host's lifetime. Read this page to decide which packages you need and to learn
the concepts the rest of the documentation uses.

## The packages

| Package | Provider alias | Use it for |
|---|---|---|
| `Invex.Extensions.Logging.FancyConsole` | `FancyConsole` | Human-friendly console output while you watch an app run. |
| `Invex.Extensions.Logging.File` | `File` | Plain-text log files that roll over and are purged automatically. |
| `Invex.Extensions.Logging.Utils` | None | A standalone logger for startup and shutdown, plus a startup message helper. |

The packages are independent. Use either provider on its own, both together, or alongside the
built-in providers.

## When to use it

- **You read logs as a person.** FancyConsole favors readability: layouts, colors, scopes, and
  pretty exceptions.
- **You want local log files without a logging framework.** The File provider needs no sinks or
  pipelines; register it and it writes rolling files.
- **You split logs by concern.** Route a scoped group (such as `Orders`) or a level (such as
  `Error`) to its own file.
- **You need to log before the host exists.** Utils gives you a logger for host builder failures and
  shutdown messages.

## When not to use it

- **Structured or machine-first output.** Neither provider writes JSON or keeps structured
  properties. Use a provider with a structured format when a machine must parse each entry exactly.
- **Hard disk quotas.** File size and retention settings are thresholds checked on write, not quotas.
- **Guaranteed durability.** Buffered entries can be lost if the process crashes, and even direct
  writes hand bytes to the operating system without requesting a durable storage flush.
- **Remote log shipping.** Files are written to a local directory only.

## Key concepts at a glance

| Concept | Meaning |
|---|---|
| Provider alias | `FancyConsole` and `File` name the configuration sections (`Logging:FancyConsole`, `Logging:File`) and scope filter rules to one provider. |
| Options binding | Options bind from configuration, then code delegates passed to `AddFancyConsole` or `AddFile` override them. Reloadable sources apply changes to later entries. |
| Level filtering | Done by the standard logging rules; the providers never filter levels themselves. |
| Layout | How FancyConsole renders an entry: `Standard`, `SingleLine`, `Minimal`, or `Detailed`. |
| Resolved file name | `LogName` plus any mapped group suffix and level suffix, e.g. `my-app_orders_errors.log`. |
| Group | A scope property named `Group`, usually set with `BeginGroupScope`, that selects a file suffix. |
| Rollover | Renaming the active file to a timestamped archive when it reaches the size or age threshold. |
| Retention | Deleting the oldest archive of a resolved file name when its archives reach `MaxTotalSizeBytes`. |
| Write mode | Buffered (background thread, the default) or direct (on the calling thread). |
| Host logger | A `HostLogger` that owns its own factory, independent of the application host. |

## Design principles

- **Standard pipeline first.** Registration, configuration, reload, scopes, and filtering all use the
  `Microsoft.Extensions.Logging` mechanisms you already know.
- **Logging never breaks the application.** Rendering and write failures are reported to debug or
  console output instead of being thrown; the file writer retries and then drops the entry.
- **Plain, predictable output.** File lines have a fixed prefix, and console text is written
  literally; Spectre markup in messages is not interpreted.
- **Thresholds, not guarantees.** Size and retention limits are checked when writing, and the
  documentation states where they can be exceeded.

## See also

- [Getting Started](getting-started.md)
- [Fancy Console Logger](fancy-console.md)
- [File Logger Configuration](configuration.md)
- [Logging Utilities](logging-utilities.md)
- <xref:Invex.Extensions.Logging.FancyConsole.FancyConsoleLoggerExtensions>
- <xref:Invex.Extensions.Logging.File.FileLoggerExtension>
- <xref:Invex.Extensions.Logging.Utils.LogUtil>
