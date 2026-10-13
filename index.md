# Invex.Extensions.Logging

Useful utilities for [`Microsoft.Extensions.Logging`](https://learn.microsoft.com/dotnet/core/extensions/logging):
a readable, styled console logger, a rolling file logger, and helpers for logging before the host
starts. The providers plug into the standard logging pipeline, so they work with ASP.NET Core, the
Generic Host, or a manually created `ILoggerFactory`.

## Start here

- [Introduction](docs/introduction.md): what each package is for, and when to use it.
- [Getting Started](docs/getting-started.md): install, register the providers, and see the defaults.
- [Fancy Console Logger](docs/fancy-console.md): layouts, styles, scopes, and exceptions.
- [File Logger Configuration](docs/configuration.md): file options and routing by group and level.
- [API Reference](api/index.md): every public type and member.

## Packages

| Package | What it does |
|---|---|
| [`Invex.Extensions.Logging.FancyConsole`](https://www.nuget.org/packages/Invex.Extensions.Logging.FancyConsole) | A colorful, readable console logger built on [Spectre.Console](https://spectreconsole.net/), with multiple layouts, per-level styles, scopes, pretty exceptions, and standard-error routing. |
| [`Invex.Extensions.Logging.File`](https://www.nuget.org/packages/Invex.Extensions.Logging.File) | A dependency-light file logger with size- and time-based rollover, retention limits, routing by scoped group and log level, and buffered or synchronous writing. |
| [`Invex.Extensions.Logging.Utils`](https://www.nuget.org/packages/Invex.Extensions.Logging.Utils) | Helpers for creating a logger before a host is built, owning its factory, and logging startup information. |
