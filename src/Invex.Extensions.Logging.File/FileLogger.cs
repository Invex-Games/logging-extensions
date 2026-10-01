namespace Invex.Extensions.Logging.File;

/// <summary>
///     An <see cref="ILogger" /> implementation that formats log entries and forwards them to an
///     <see cref="IFileLogWriter" /> for persistence.
/// </summary>
/// <param name="name">The logger category name, included in each formatted log entry.</param>
/// <param name="logWriter">The writer responsible for persisting formatted entries to disk.</param>
/// <param name="getScopeProvider">Gets the current scope provider shared by the logger's categories.</param>
/// <remarks>
///     Each entry is formatted as
///     <c>[{timestamp} {level} {category}] {message}</c>, where the timestamp uses the local time of the
///     writer's <see cref="TimeProvider" /> in <c>yyyy-MM-dd HH:mm:ss.fff zzz</c> format and the level is a
///     three-letter code (<c>TRC</c>, <c>DBG</c>, <c>INF</c>, <c>WRN</c>, <c>ERR</c>, or <c>CRT</c>).
///     The innermost nonempty string <c>Group</c> scope property controls file routing. Scope data is not
///     included in the formatted entry.
///     Level filtering is delegated to the logging framework, so <see cref="IsEnabled" /> always returns
///     <see langword="true" />.
/// </remarks>
internal sealed class FileLogger(string name, IFileLogWriter logWriter, Func<IExternalScopeProvider> getScopeProvider)
    : ILogger
{
    /// <summary>
    ///     The structured scope property used to supply a file routing group.
    /// </summary>
    internal const string GroupScopeKey = "Group";

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull =>
        getScopeProvider()
            .Push(state);

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

        string? group = null;

        if (FileLogWriterUtil.TryWrite(() => group = GetGroup()))
            logWriter.Log(log, logLevel, group);
    }

    /// <summary>
    ///     Captures the innermost nonempty string group before an entry can be queued for writing.
    /// </summary>
    /// <returns>The group supplied by the current scopes, or <see langword="null" /> if none is supplied.</returns>
    private string? GetGroup()
    {
        var group = new StrongBox<string?>();

        getScopeProvider()
            .ForEachScope(static (scope, currentGroup) =>
                {
                    if (scope is not IEnumerable<KeyValuePair<string, object?>> properties)
                        return;

                    foreach (var property in properties)
                        if (property is { Key: GroupScopeKey, Value: string { Length: > 0 } name })
                            currentGroup.Value = name;
                },
                group);

        return group.Value;
    }
}
