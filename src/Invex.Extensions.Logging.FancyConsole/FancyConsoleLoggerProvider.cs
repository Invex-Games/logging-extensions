namespace Invex.Extensions.Logging.FancyConsole;

/// <summary>
///     An <see cref="ILoggerProvider" /> that writes log entries to the console using Spectre.Console, formatted
///     according to <see cref="FancyConsoleLoggerConfiguration" />.
/// </summary>
/// <remarks>
///     One <see cref="FancyConsoleLogger" /> is cached per category. Configuration is tracked via
///     <see cref="IOptionsMonitor{TOptionsMonitor}" /> and re-read for every entry, so runtime changes apply to
///     subsequent entries. Writes from all loggers are serialized so entries are never interleaved.
/// </remarks>
[ProviderAlias("FancyConsole")]
internal sealed class FancyConsoleLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    /// <summary>
    ///     Serializes console writes across all providers, since they may share the same console.
    /// </summary>
    private static readonly object WriteLock = new();

    /// <summary>
    ///     The console that receives entries below <see cref="FancyConsoleLoggerConfiguration.LogToStandardErrorThreshold" />.
    /// </summary>
    private readonly IAnsiConsole _console;

    /// <summary>
    ///     The console that receives entries at or above
    ///     <see cref="FancyConsoleLoggerConfiguration.LogToStandardErrorThreshold" />.
    /// </summary>
    private readonly IAnsiConsole _errorConsole;

    /// <summary>
    ///     The cached loggers, keyed by category name.
    /// </summary>
    private readonly ConcurrentDictionary<string, FancyConsoleLogger> _loggers = new(StringComparer.Ordinal);

    /// <summary>
    ///     The registration for configuration change notifications.
    /// </summary>
    private readonly IDisposable? _onChangeToken;

    /// <summary>
    ///     Initializes a new instance of the <see cref="FancyConsoleLoggerProvider" /> class, capturing the current
    ///     values of <see cref="Console" />, <see cref="ErrorConsole" />, and <see cref="TimeProvider" />.
    /// </summary>
    /// <param name="config">The configuration monitor.</param>
    public FancyConsoleLoggerProvider(IOptionsMonitor<FancyConsoleLoggerConfiguration> config)
    {
        _console = Console;
        _errorConsole = ErrorConsole;
        Clock = TimeProvider;
        CurrentConfig = config.CurrentValue;
        _onChangeToken = config.OnChange(updatedConfig => CurrentConfig = updatedConfig);
    }

    /// <summary>
    ///     Gets or sets the console used for standard output. Captured when a provider is constructed; intended
    ///     for testing.
    /// </summary>
    public static IAnsiConsole Console { get; set; } = AnsiConsole.Console;

    /// <summary>
    ///     Gets or sets the console used for standard error. Captured when a provider is constructed; intended for
    ///     testing.
    /// </summary>
    public static IAnsiConsole ErrorConsole { get; set; } = AnsiConsole.Create(new()
    {
        Out = new AnsiConsoleOutput(System.Console.Error),
    });

    /// <summary>
    ///     Gets or sets the time provider used to timestamp entries. Captured when a provider is constructed;
    ///     intended for testing.
    /// </summary>
    public static TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>
    ///     Gets the most recent configuration.
    /// </summary>
    internal FancyConsoleLoggerConfiguration CurrentConfig { get; private set; }

    /// <summary>
    ///     Gets the scope provider supplied by the logging infrastructure.
    /// </summary>
    internal IExternalScopeProvider ScopeProvider { get; private set; } = NullExternalScopeProvider.Instance;

    /// <summary>
    ///     Gets the time provider used to timestamp entries.
    /// </summary>
    internal TimeProvider Clock { get; }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        #if NET8_0_OR_GREATER
        return _loggers.GetOrAdd(categoryName, name => new(name, this));
        #else
        return _loggers.GetOrAdd(categoryName, name => new(name, this))!;
        #endif
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _loggers.Clear();
        _onChangeToken?.Dispose();
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider) =>
        ScopeProvider = scopeProvider;

    /// <summary>
    ///     Formats and writes an entry to standard output or standard error, depending on
    ///     <see cref="FancyConsoleLoggerConfiguration.LogToStandardErrorThreshold" />.
    /// </summary>
    /// <param name="entry">The entry to write.</param>
    /// <param name="config">The configuration to format the entry with.</param>
    internal void Write(FancyConsoleLogEntry entry, FancyConsoleLoggerConfiguration config)
    {
        var renderables = FancyConsoleFormatter.Format(entry, config);

        var console = config.LogToStandardErrorThreshold != LogLevel.None &&
                      entry.LogLevel >= config.LogToStandardErrorThreshold
            ? _errorConsole
            : _console;

        lock (WriteLock)
        {
            foreach (var renderable in renderables)
                console.Write(renderable);
        }
    }

    /// <summary>
    ///     A scope provider that tracks no scopes, used until the logging infrastructure supplies one.
    /// </summary>
    private sealed class NullExternalScopeProvider : IExternalScopeProvider
    {
        /// <summary>
        ///     The shared instance.
        /// </summary>
        public static readonly NullExternalScopeProvider Instance = new();

        /// <inheritdoc />
        public void ForEachScope<TState>(Action<object?, TState> callback, TState state) { }

        /// <inheritdoc />
        public IDisposable Push(object? state) =>
            NullScope.Instance;
    }

    /// <summary>
    ///     A scope that does nothing when disposed.
    /// </summary>
    private sealed class NullScope : IDisposable
    {
        /// <summary>
        ///     The shared instance.
        /// </summary>
        public static readonly NullScope Instance = new();

        /// <inheritdoc />
        public void Dispose() { }
    }
}
