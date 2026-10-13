# API Reference

Generated reference for the Invex.Extensions.Logging packages, produced from the XML documentation
comments in the source code.

## Invex.Extensions.Logging.FancyConsole

| Type | Kind | Description |
|---|---|---|
| [`FancyConsoleLoggerExtensions`](xref:Invex.Extensions.Logging.FancyConsole.FancyConsoleLoggerExtensions) | Static class | `AddFancyConsole` extension methods for registering the fancy console logger with an `ILoggingBuilder`. |
| [`FancyConsoleLoggerConfiguration`](xref:Invex.Extensions.Logging.FancyConsole.Configuration.FancyConsoleLoggerConfiguration) | Class | Options controlling the layout, timestamps, scopes, styles, exceptions, and standard-error routing. |
| [`FancyConsoleLayout`](xref:Invex.Extensions.Logging.FancyConsole.Configuration.FancyConsoleLayout) | Enum | The layout used to render each log entry. |
| [`FancyConsoleExceptionFormat`](xref:Invex.Extensions.Logging.FancyConsole.Configuration.FancyConsoleExceptionFormat) | Enum | How exceptions attached to log entries are rendered. |

## Invex.Extensions.Logging.File

| Type | Kind | Description |
|---|---|---|
| [`FileLoggerExtension`](xref:Invex.Extensions.Logging.File.FileLoggerExtension) | Static class | `AddFile` registration methods and `BeginGroupScope` for routing scoped entries. |
| [`FileLoggerConfiguration`](xref:Invex.Extensions.Logging.File.Configuration.FileLoggerConfiguration) | Class | Options controlling the log directory, file names, rollover, and retention. |
| [`FileRolloverInterval`](xref:Invex.Extensions.Logging.File.Configuration.FileRolloverInterval) | Enum | The time interval after which the active log file is rolled over. |

## Invex.Extensions.Logging.Utils

| Type | Kind | Description |
|---|---|---|
| [`LogUtil`](xref:Invex.Extensions.Logging.Utils.LogUtil) | Static class | Creates standalone logger factories and host loggers, and emits startup information. |
| [`HostLogger`](xref:Invex.Extensions.Logging.Utils.HostLogger) | Class | An `ILogger` wrapper that owns and disposes its factory, with a startup information helper. |

## Typical flow

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;
using Invex.Extensions.Logging.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using var hostLogger = LogUtil.CreateHostLogger(logging => logging.AddFancyConsole());

var builder = Host.CreateApplicationBuilder(args);
hostLogger.LogStartupInfo<Program>(builder.Environment);

builder.Logging.ClearProviders().AddFancyConsole().AddFile();

using var host = builder.Build();
await host.RunAsync();
```

For guided usage, start with the [Introduction](../docs/introduction.md), or see
[Logging Utilities](../docs/logging-utilities.md) for examples and ownership rules.
