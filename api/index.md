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
| [`FileLoggerExtension`](xref:Invex.Extensions.Logging.File.FileLoggerExtension) | `AddFile` registration methods and `BeginGroupScope` for routing scoped entries. |
| [`FileLoggerConfiguration`](xref:Invex.Extensions.Logging.File.Configuration.FileLoggerConfiguration) | Options controlling the log directory, file names, rollover, and retention. |
| [`FileRolloverInterval`](xref:Invex.Extensions.Logging.File.Configuration.FileRolloverInterval) | The time interval after which the active log file is rolled over. |

## Invex.Extensions.Logging.Utils

### Public types

| Type | Description |
|------|-------------|
| [`LogUtil`](xref:Invex.Extensions.Logging.Utils.LogUtil) | Creates standalone logger factories and host loggers, and emits startup information. |
| [`HostLogger`](xref:Invex.Extensions.Logging.Utils.HostLogger) | An `ILogger` wrapper that owns and disposes its factory, with a startup information helper. |

See [logging utilities](../docs/logging-utilities.md) for examples and ownership rules.

> [!NOTE]
> [DocFX](https://dotnet.github.io/docfx/) generates this reference from the compiled libraries and their
> XML documentation. Assembly metadata includes the registration methods declared in C# 14 extension
> blocks, which source-based extraction in DocFX 2.78.5 omits. Build first so the API and comments are current.

From the repository root:

```shell
dotnet build Invex.Extensions.Logging.slnx --configuration Release
docfx docfx.json --warningsAsErrors
```

The site is generated in `_site/`. Add `--serve` to the DocFX command to preview it locally. Files under
`api/` with a `.yml` extension are generated; edit source XML comments or Markdown pages instead.

The Atom `BuildDocs` target also generates the site from existing Release assemblies; it does not
compile the libraries. Run the Release build above first, including before using `ServeDocs`.

