# API Reference

Generated API reference for the Invex Logging Extensions packages.

## Invex.Extensions.Logging.FancyConsole

### Public types

| Type | Description |
|------|-------------|
| [`FancyConsoleLoggerExtensions`](xref:Invex.Extensions.Logging.FancyConsole.FancyConsoleLoggerExtensions) | `AddFancyConsole` extension methods for registering the fancy console logger with an `ILoggingBuilder`. |
| [`FancyConsoleLoggerConfiguration`](xref:Invex.Extensions.Logging.FancyConsole.Configuration.FancyConsoleLoggerConfiguration) | Options controlling the layout, timestamps, scopes, styles, exceptions, and standard-error routing. |
| [`FancyConsoleLayout`](xref:Invex.Extensions.Logging.FancyConsole.Configuration.FancyConsoleLayout) | The layout used to render each log entry. |
| [`FancyConsoleExceptionFormat`](xref:Invex.Extensions.Logging.FancyConsole.Configuration.FancyConsoleExceptionFormat) | How exceptions attached to log entries are rendered. |

## Invex.Extensions.Logging.File

### Public types

| Type | Description |
|------|-------------|
| [`FileLoggerExtension`](xref:Invex.Extensions.Logging.File.FileLoggerExtension) | `AddFile` extension methods for registering the file logger with an `ILoggingBuilder`. |
| [`FileLoggerConfiguration`](xref:Invex.Extensions.Logging.File.Configuration.FileLoggerConfiguration) | Options controlling the log directory, file names, rollover, and retention. |
| [`FileRolloverInterval`](xref:Invex.Extensions.Logging.File.Configuration.FileRolloverInterval) | The time interval after which the active log file is rolled over. |

> [!NOTE]
> This reference is generated from the XML documentation comments in the source code by
> [DocFX](https://dotnet.github.io/docfx/). To regenerate locally, run `docfx docfx.json` from the repository
> root.

