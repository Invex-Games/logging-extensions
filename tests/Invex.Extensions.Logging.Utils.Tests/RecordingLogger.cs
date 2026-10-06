namespace Invex.Extensions.Logging.Utils.Tests;

/// <summary>
///     Captures entries and enabled checks without writing to an external destination.
/// </summary>
internal sealed class RecordingLogger : ILogger
{
    /// <summary>
    ///     Gets or sets whether logging is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    ///     Gets the levels queried by callers.
    /// </summary>
    public List<LogLevel> EnabledChecks { get; } = [];

    /// <summary>
    ///     Gets the captured entries.
    /// </summary>
    public List<RecordedLogEntry> Entries { get; } = [];

    /// <summary>
    ///     Records a log entry, including its structured state and rendered message.
    /// </summary>
    /// <typeparam name="TState">The state type.</typeparam>
    /// <param name="logLevel">The entry's level.</param>
    /// <param name="eventId">The entry's event identifier.</param>
    /// <param name="state">The entry's state.</param>
    /// <param name="exception">The entry's exception.</param>
    /// <param name="formatter">The message formatter.</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add(new(logLevel, eventId, state!, exception, formatter(state, exception)));

    /// <summary>
    ///     Records the requested level and returns the configured enabled state.
    /// </summary>
    /// <param name="logLevel">The requested level.</param>
    /// <returns>The configured enabled state.</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        EnabledChecks.Add(logLevel);

        return Enabled;
    }

    /// <summary>
    ///     Returns no scope token because these tests capture entries without scopes.
    /// </summary>
    /// <typeparam name="TState">The scope state type.</typeparam>
    /// <param name="state">The scope state.</param>
    /// <returns>No scope token.</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        null;
}

/// <summary>
///     Stores a captured entry before provider-specific formatting.
/// </summary>
/// <param name="level">The entry's level.</param>
/// <param name="eventId">The entry's event identifier.</param>
/// <param name="state">The original state.</param>
/// <param name="exception">The original exception.</param>
/// <param name="message">The formatted message.</param>
internal sealed class RecordedLogEntry(
    LogLevel level,
    EventId eventId,
    object state,
    Exception? exception,
    string message
)
{
    /// <summary>
    ///     Gets the level.
    /// </summary>
    public LogLevel Level { get; } = level;

    /// <summary>
    ///     Gets the event identifier.
    /// </summary>
    public EventId EventId { get; } = eventId;

    /// <summary>
    ///     Gets the original state, including structured properties.
    /// </summary>
    public object State { get; } = state;

    /// <summary>
    ///     Gets the original exception.
    /// </summary>
    public Exception? Exception { get; } = exception;

    /// <summary>
    ///     Gets the formatted message.
    /// </summary>
    public string Message { get; } = message;
}
