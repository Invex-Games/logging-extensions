namespace Invex.Extensions.Logging.FancyConsole;

/// <summary>
///     The data captured for a single log entry, passed to <see cref="FancyConsoleFormatter" /> for rendering.
/// </summary>
/// <param name="Timestamp">The time the entry was logged, in local time or UTC according to configuration.</param>
/// <param name="LogLevel">The level of the entry.</param>
/// <param name="Category">The logger category name.</param>
/// <param name="EventId">The event ID supplied with the entry.</param>
/// <param name="Message">The formatted, nonempty message.</param>
/// <param name="Exception">The exception supplied with the entry, if any.</param>
/// <param name="Scopes">The formatted active scopes, outermost first.</param>
/// <param name="ThreadId">The managed thread ID of the logging thread.</param>
internal sealed record FancyConsoleLogEntry(
    DateTimeOffset Timestamp,
    LogLevel LogLevel,
    string Category,
    EventId EventId,
    string Message,
    Exception? Exception,
    IReadOnlyList<string> Scopes,
    int ThreadId
);
