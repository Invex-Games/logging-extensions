namespace Invex.Extensions.Logging.File.Provider;

/// <summary>
///     Base class for file logger providers. Caches one <see cref="FileLogger" /> per category name and
///     tracks the current <see cref="FileLoggerConfiguration" />, reacting to configuration changes at runtime.
/// </summary>
internal abstract class FileLoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDisposable? _onChangeToken;
    private FileLoggerConfiguration _currentConfig;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FileLoggerProvider" /> class and subscribes to
    ///     configuration change notifications.
    /// </summary>
    /// <param name="config">The options monitor providing the current logger configuration.</param>
    protected FileLoggerProvider(IOptionsMonitor<FileLoggerConfiguration> config)
    {
        _currentConfig = config.CurrentValue;
        _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
    }

    /// <summary>
    ///     Gets the writer used by all loggers created by this provider to persist log entries.
    /// </summary>
    protected abstract IFileLogWriter LogWriter { get; }

    /// <summary>
    ///     Creates (or returns a cached) <see cref="FileLogger" /> for the given category, ensuring the
    ///     underlying <see cref="LogWriter" /> has been started. Category names are compared
    ///     case-insensitively.
    /// </summary>
    /// <param name="categoryName">The category name of the logger, typically the fully qualified type name.</param>
    /// <returns>An <see cref="ILogger" /> for the given category.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        LogWriter.Start();

        #if NET8_0_OR_GREATER
        return _loggers.GetOrAdd(categoryName, name => new(name, LogWriter));
        #else
        return _loggers.GetOrAdd(categoryName, name => new(name, LogWriter))!;
        #endif
    }

    /// <summary>
    ///     Gets the most recent <see cref="FileLoggerConfiguration" />, reflecting any runtime
    ///     configuration changes.
    /// </summary>
    protected FileLoggerConfiguration GetCurrentConfig() =>
        _currentConfig;

    /// <summary>
    ///     Disposes the <see cref="LogWriter" /> (flushing any pending entries), clears the logger cache,
    ///     and unsubscribes from configuration change notifications.
    /// </summary>
    public virtual void Dispose()
    {
        LogWriter.Dispose();
        _loggers.Clear();
        _onChangeToken?.Dispose();
    }
}
