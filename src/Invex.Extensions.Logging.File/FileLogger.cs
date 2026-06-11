namespace Invex.Extensions.Logging.File;

/// <summary>
///     An <see cref="ILogger" /> implementation that formats log entries and forwards them to an
///     <see cref="IFileLogWriter" /> for persistence.
/// </summary>
/// <param name="name">The logger category name, included in each formatted log entry.</param>
/// <param name="logWriter">The writer responsible for persisting formatted entries to disk.</param>
/// <remarks>
///     Each entry is formatted as
///     <c>[{timestamp} {level} {category}] {message}</c>, where the timestamp uses the local time of the
///     writer's <see cref="TimeProvider" /> in <c>yyyy-MM-dd HH:mm:ss.fff zzz</c> format and the level is a
///     three-letter code (<c>TRC</c>, <c>DBG</c>, <c>INF</c>, <c>WRN</c>, <c>ERR</c>, or <c>CRT</c>).
///     Scopes are not supported; <see cref="BeginScope{TState}" /> returns <see langword="null" />.
///     Level filtering is delegated to the logging framework, so <see cref="IsEnabled" /> always returns
///     <see langword="true" />.
/// </remarks>
internal sealed class FileLogger(string name, IFileLogWriter logWriter) : ILogger
{
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) =>
        true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var logMessage = formatter(state, exception);

        if (logMessage is null or "")
            return;

        var now = logWriter
            .TimeProvider
            .GetLocalNow()
            .ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

        var logLevelCode = logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???",
        };

        var log = $"[{now} {logLevelCode} {name}] {logMessage}{Environment.NewLine}";

        logWriter.Log(log, logLevel);
    }
}
