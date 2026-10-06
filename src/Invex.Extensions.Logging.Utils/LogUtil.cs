namespace Invex.Extensions.Logging.Utils;

/// <summary>
///     Creates standalone loggers for host lifecycle messages and writes application startup information.
/// </summary>
/// <remarks>
///     The factories created here are independent of any application's host and dependency injection container.
///     Register logging providers in the configuration callback and dispose the returned logger or factory when
///     it is no longer needed.
/// </remarks>
[PublicAPI]
public static class LogUtil
{
    /// <summary>
    ///     Creates a logger with the category <c>Host</c> and an owned, standalone logger factory.
    /// </summary>
    /// <param name="configure">
    ///     An optional callback that registers providers and configures logging after the default
    ///     <c>Microsoft</c> category filter has been added. No providers are registered automatically.
    /// </param>
    /// <returns>A logger that disposes its factory and the factory-owned providers when disposed.</returns>
    /// <remarks>
    ///     Use this logger before building a host or after stopping one. It does not inherit the host's providers,
    ///     configuration, or services. The default category filter permits <c>Microsoft</c> entries at
    ///     <see cref="LogLevel.Warning" /> and above; the callback can add further filtering rules.
    /// </remarks>
    public static HostLogger CreateHostLogger(Action<ILoggingBuilder>? configure = null)
    {
        var loggerFactory = CreateHostLoggerFactory(configure);
        var logger = loggerFactory.CreateLogger("Host");

        return new(logger, loggerFactory);
    }

    /// <summary>
    ///     Creates a standalone logger factory with a <c>Microsoft</c> category filter at
    ///     <see cref="LogLevel.Warning" />.
    /// </summary>
    /// <param name="configure">
    ///     An optional callback that registers providers and configures logging after the default filter is added.
    ///     No providers are registered automatically.
    /// </param>
    /// <returns>A standalone factory that the caller must dispose when it is no longer needed.</returns>
    /// <remarks>
    ///     The factory does not automatically load host configuration or use a host's services. Use the callback
    ///     to register providers, bind configuration if required, and add filtering rules. Disposing the factory
    ///     disposes providers that it owns, including providers created by its dependency injection container.
    /// </remarks>
    public static ILoggerFactory CreateHostLoggerFactory(Action<ILoggingBuilder>? configure = null)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddFilter("Microsoft", LogLevel.Warning);
            configure?.Invoke(builder);
        });

        return loggerFactory;
    }

    /// <summary>
    ///     Writes an information-level startup message using the assembly containing <typeparamref name="T" />.
    /// </summary>
    /// <typeparam name="T">A type whose assembly supplies the application name and assembly version.</typeparam>
    /// <param name="logger">The logger that receives the startup message.</param>
    /// <param name="environment">
    ///     An optional host environment whose <see cref="IHostEnvironment.EnvironmentName" /> is included in the
    ///     message. Its <see cref="IHostEnvironment.ApplicationName" /> does not supply the application name.
    /// </param>
    /// <remarks>
    ///     Uses the assembly's simple name and <c>AssemblyName.Version</c>, rather than its file or informational
    ///     version, and <see cref="Environment.MachineName" />. The explicit-value overload determines formatting
    ///     and skips the message when <see cref="LogLevel.Information" /> is disabled. Calling this method does
    ///     not start a host or verify that it started successfully.
    /// </remarks>
    public static void LogStartupInfo<T>(ILogger logger, IHostEnvironment? environment = null)
    {
        var assembly = typeof(T).Assembly;

        var applicationName = assembly.GetName()
            .Name;

        var version = assembly
            .GetName()
            .Version
            ?.ToString();

        var machineName = Environment.MachineName;
        var environmentName = environment?.EnvironmentName;

        LogStartupInfo(logger, applicationName, version, machineName, environmentName);
    }

    /// <summary>
    ///     Writes an information-level startup message with explicit application, version, machine, and environment
    ///     values, if that level is enabled.
    /// </summary>
    /// <param name="logger">The logger that receives the startup message.</param>
    /// <param name="applicationName">The application name, or <see langword="null" />/empty to display <c>Application</c>.</param>
    /// <param name="version">
    ///     The version, or <see langword="null" />/empty to omit it. Leading <c>v</c> and <c>V</c> characters are removed
    ///     before a single <c>v</c> prefix is added.
    /// </param>
    /// <param name="machineName">The machine name, or <see langword="null" />/empty to omit the <c>on</c> clause.</param>
    /// <param name="environment">
    ///     The environment name, or <see langword="null" />/empty to omit the <c>in ... configuration</c> clause.
    /// </param>
    /// <remarks>
    ///     The current message order is <c>Started {application} v{version} in {environment} configuration on {machine}</c>.
    ///     Optional clauses include their own leading space, and whitespace-only values are retained. The template is
    ///     <c>Started{AppName}{Version}{MachineName}{Configuration}</c>: <c>MachineName</c> currently receives the
    ///     environment clause, and <c>Configuration</c> receives the machine clause. Structured values include these
    ///     formatted clauses rather than the raw inputs. Logging failures from the supplied logger are not caught.
    /// </remarks>
    public static void LogStartupInfo(
        ILogger logger,
        string? applicationName,
        string? version,
        string? machineName,
        string? environment)
    {
        if (!logger.IsEnabled(LogLevel.Information))
            return;

        var applicationNameDisplay = applicationName is { Length: > 0 }
            ? $" {applicationName}"
            : " Application";

        var versionDisplay = version is { Length: > 0 }
            ? $" v{version.TrimStart('v', 'V')}"
            : string.Empty;

        var machineNameDisplay = machineName is { Length: > 0 }
            ? $" on {machineName}"
            : string.Empty;

        var environmentDisplay = environment is { Length: > 0 }
            ? $" in {environment} configuration"
            : string.Empty;

        logger.LogInformation("Started{AppName}{Version}{MachineName}{Environment}",
            applicationNameDisplay,
            versionDisplay,
            machineNameDisplay,
            environmentDisplay);
    }
}
