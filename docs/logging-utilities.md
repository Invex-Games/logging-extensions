# Logging Utilities

`Invex.Extensions.Logging.Utils` provides a standalone logger for messages written before a host is built
or after it stops, plus helpers for a consistent startup message. It targets `net10.0`, `net9.0`, `net8.0`,
and `netstandard2.0`.

The two public types are `LogUtil` and `HostLogger`. They use the standard `Microsoft.Extensions.Logging`
interfaces; they do not provide a logging destination themselves.

## Create a lifecycle logger

Register the providers you need in the callback. This example uses FancyConsole for startup and shutdown
messages, independently of the providers configured on the application host:

```csharp
using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.Utils;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using var hostLogger = LogUtil.CreateHostLogger(logging => logging.AddFancyConsole());

var builder = Host.CreateApplicationBuilder(args);
hostLogger.LogStartupInfo<Program>(builder.Environment);
hostLogger.LogInformation("Configuring services...");

builder.Logging.ClearProviders();
builder.Logging.AddFancyConsole();

using var host = builder.Build();
host.Run();

hostLogger.LogInformation("Stopped.");
```

`CreateHostLogger` creates a logger with category `Host` backed by its own factory. It can be created before
`Host.CreateApplicationBuilder` if you need to log failures during host builder creation. Passing
`builder.Environment` to the startup helper is optional.

The factory adds a category filter for `Microsoft` at `Warning` before invoking the callback. This rule
does not set a minimum for other categories. The callback can register providers, set minimum levels, and
add more filtering rules. To replace that category rule, add a later rule for the same category:

```csharp
using var hostLogger = LogUtil.CreateHostLogger(logging =>
{
    logging.AddFancyConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddFilter("Microsoft", LogLevel.Error);
});
```

Calling `CreateHostLogger()` without a callback registers no providers, so entries have no output
destination. This factory does not inherit the application's host configuration, providers, or services.
If you want settings from configuration, bind them explicitly in the callback. Registering providers on
`builder.Logging` does not configure `hostLogger`, and configuring `hostLogger` does not configure the
host's loggers.

## Use a factory for other categories

`CreateHostLoggerFactory` applies the same filter and callback but returns an `ILoggerFactory`, allowing
you to choose categories yourself:

```csharp
using var loggerFactory = LogUtil.CreateHostLoggerFactory(logging => logging.AddFancyConsole());
var logger = loggerFactory.CreateLogger("Bootstrap");

logger.LogInformation("Loading application settings...");
```

The caller owns this factory and must dispose it. `HostLogger` instead owns the factory supplied to its
constructor: disposing the wrapper disposes that factory and its owned providers. Avoid passing a shared
application factory to `new HostLogger(...)` unless this ownership is intentional, since disposal can
affect every logger using that factory.

Dispose the lifecycle logger after writing the final shutdown message. For a buffered file provider,
factory disposal drains queued entries. If both the host and lifecycle factory use `AddFile`, configure
distinct `LogName` values when they should write to separate files.

`HostLogger` forwards `Log`, `IsEnabled`, and `BeginScope` to its wrapped logger. It adds no further
filtering or exception handling; failures from a custom wrapped logger or factory can propagate.

## Startup information

Use a type from your application assembly to obtain its name and version:

```csharp
hostLogger.LogStartupInfo<Program>(builder.Environment);

// The same helper works with any ILogger.
LogUtil.LogStartupInfo<Program>(logger, builder.Environment);
```

The generic helper reads:

| Value | Source |
|-------|--------|
| Application name | Simple name of the assembly containing `T`. |
| Version | That assembly's `AssemblyName.Version`, including the components returned by `ToString()`. |
| Machine name | `Environment.MachineName`. |
| Environment | `IHostEnvironment.EnvironmentName`, when an environment was supplied. |

The name comes from the assembly rather than `IHostEnvironment.ApplicationName`. The version is the
assembly version rather than the file, package, or informational version. Choose `T` from your executable
assembly to describe the application; a type from a library describes that library instead.

To supply your own values, use the explicit overload:

```csharp
LogUtil.LogStartupInfo(
    logger,
    applicationName: "OrderWorker",
    version: "v2.3.0",
    machineName: "worker-01",
    environment: "Production");
```

The current formatted message is:

```text
Started OrderWorker v2.3.0 in Production configuration on worker-01
```

The provider may add its own timestamp, level, category, or layout around this message. The helper emits
one `Information` entry only when `logger.IsEnabled(LogLevel.Information)` returns `true`. Calling it
does not start a host or establish that startup succeeded; choose the call site to match the meaning of
the message in your application.

Formatting rules:

- A null or empty application name becomes `Application`.
- A null or empty version is omitted. Otherwise, all leading `v` and `V` characters are removed and one
  lowercase `v` is prepended.
- A null or empty environment omits `in ... configuration`.
- A null or empty machine name omits `on ...`.
- Whitespace-only strings are included; values are not otherwise trimmed.

When all explicit values are null or empty, the message is `Started Application`.

The structured template is `Started{AppName}{Version}{MachineName}{Configuration}`. Its values contain
the display clauses, including their leading spaces. In the current implementation, `MachineName`
receives the environment clause (` in Production configuration`) and `Configuration` receives the
machine clause (` on worker-01`). Account for this ordering if you consume the structured fields directly.

See the [fancy console guide](fancy-console.md) and [file logger configuration](configuration.md) for
provider settings, or [buffering](buffering.md) for file writer shutdown behavior.
