namespace Invex.Extensions.Logging.FancyConsole;

/// <summary>
///     An <see cref="ILogger" /> for a single category that captures log entries and passes them to its
///     <see cref="FancyConsoleLoggerProvider" /> for formatting and writing.
/// </summary>
/// <param name="name">The category name.</param>
/// <param name="provider">The provider that owns this logger.</param>
/// <remarks>
///     Logging never throws: any failure while formatting or writing an entry is reported to debug output and
///     standard error, and the entry is dropped.
/// </remarks>
internal sealed class FancyConsoleLogger(string name, FancyConsoleLoggerProvider provider) : ILogger
{
    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        provider.ScopeProvider.Push(state);

    /// <summary>
    ///     Checks if the given <paramref name="logLevel" /> is enabled. Every level except
    ///     <see cref="LogLevel.None" /> is enabled; filtering is left to the logging framework.
    /// </summary>
    /// <param name="logLevel">The level to check.</param>
    /// <returns><see langword="true" /> unless <paramref name="logLevel" /> is <see cref="LogLevel.None" />.</returns>
    public bool IsEnabled(LogLevel logLevel) =>
        logLevel != LogLevel.None;

    /// <summary>
    ///     Writes a log entry to the console. Entries whose formatted message is <see langword="null" /> or empty
    ///     are skipped.
    /// </summary>
    /// <param name="logLevel">The level of the entry.</param>
    /// <param name="eventId">The event ID of the entry.</param>
    /// <param name="state">The state to be logged.</param>
    /// <param name="exception">The exception related to the entry, if any.</param>
    /// <param name="formatter">Creates the message from <paramref name="state" /> and <paramref name="exception" />.</param>
    /// <typeparam name="TState">The type of the state to be logged.</typeparam>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        try
        {
            var message = formatter(state, exception);

            if (message is null or "")
                return;

            var config = provider.CurrentConfig;

            var timestamp = config.UseUtcTimestamp
                ? provider.Clock.GetUtcNow()
                : provider.Clock.GetLocalNow();

            var entry = new FancyConsoleLogEntry(timestamp,
                logLevel,
                name,
                eventId,
                message,
                exception,
                config.IncludeScopes || config.Layout is FancyConsoleLayout.Detailed
                    ? CaptureScopes()
                    : [],
                Environment.CurrentManagedThreadId);

            provider.Write(entry, config);
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    /// <summary>
    ///     Captures the formatted, nonempty active scopes, outermost first.
    /// </summary>
    /// <returns>The formatted scopes.</returns>
    private List<string> CaptureScopes()
    {
        var scopes = new List<string>();

        provider.ScopeProvider.ForEachScope(static (scope, list) =>
            {
                var formatted = FancyConsoleFormatter.FormatScope(scope);

                #if NET8_0_OR_GREATER
                if (!string.IsNullOrEmpty(formatted))
                    list.Add(formatted);
                #else
                if (!string.IsNullOrEmpty(formatted))
                    list.Add(formatted!);
                #endif
            },
            scopes);

        return scopes;
    }

    /// <summary>
    ///     Reports a failure to write an entry without throwing.
    /// </summary>
    /// <param name="exception">The failure.</param>
    private static void ReportFailure(Exception exception)
    {
        var message = $"Failed to write log entry to the fancy console: {exception}";

        Debug.WriteLine(message);

        try
        {
            Console.Error.WriteLine(message);
        }
        catch
        {
            // Ignored - logging must never throw into the application.
        }
    }
}
