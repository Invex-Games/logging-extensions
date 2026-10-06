namespace Invex.Extensions.Logging.Utils;

/// <summary>
///     Delegates logging to an <see cref="ILogger" /> and owns the lifetime of an associated logger factory.
/// </summary>
/// <param name="logger">The logger that receives entries, enabled checks, and scopes.</param>
/// <param name="factory">The factory disposed when this wrapper is disposed.</param>
/// <remarks>
///     <see cref="LogUtil.CreateHostLogger" /> creates this wrapper with the category <c>Host</c>. When constructing
///     it directly, supply a factory whose ownership can be transferred to this wrapper; disposing it can also
///     affect other loggers created by that factory. The wrapper does not add filtering or catch logging failures.
/// </remarks>
[PublicAPI]
public sealed class HostLogger(ILogger logger, ILoggerFactory factory) : ILogger, IDisposable
{
    /// <summary>
    ///     Disposes the associated factory and its owned logging providers.
    /// </summary>
    /// <remarks>
    ///     This method delegates directly to the factory. Dispose after completing logging; providers such as the
    ///     buffered file logger use disposal to drain queued entries.
    /// </remarks>
    public void Dispose() =>
        factory.Dispose();

    /// <summary>
    ///     Forwards an entry to the wrapped logger.
    /// </summary>
    /// <typeparam name="TState">The type of the log entry state.</typeparam>
    /// <param name="logLevel">The severity of the entry.</param>
    /// <param name="eventId">The identifier of the event.</param>
    /// <param name="state">The entry's state.</param>
    /// <param name="exception">An optional exception associated with the entry.</param>
    /// <param name="formatter">The function that formats the state and exception as a message.</param>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        logger.Log(logLevel, eventId, state, exception, formatter);

    /// <summary>
    ///     Checks whether the wrapped logger enables the specified level.
    /// </summary>
    /// <param name="logLevel">The level to check.</param>
    /// <returns>The wrapped logger's enabled state for the specified level.</returns>
    public bool IsEnabled(LogLevel logLevel) =>
        logger.IsEnabled(logLevel);

    /// <summary>
    ///     Begins a scope on the wrapped logger.
    /// </summary>
    /// <typeparam name="TState">The type of the non-null scope state.</typeparam>
    /// <param name="state">The scope state forwarded to the logger.</param>
    /// <returns>A scope that should be disposed when complete, or <see langword="null" /> if the logger returns no scope.</returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull =>
        logger.BeginScope(state);

    /// <summary>
    ///     Writes startup information using the assembly containing <typeparamref name="T" /> and the current machine.
    /// </summary>
    /// <typeparam name="T">A type whose assembly supplies the application name and assembly version.</typeparam>
    /// <param name="environment">An optional host environment whose environment name is included in the message.</param>
    /// <remarks>
    ///     Delegates to <see cref="LogUtil.LogStartupInfo{T}(ILogger, IHostEnvironment)" />. The message is written at
    ///     <see cref="LogLevel.Information" /> only when enabled, and does not verify host startup.
    /// </remarks>
    public void LogStartupInfo<T>(IHostEnvironment? environment = null) =>
        LogUtil.LogStartupInfo<T>(logger, environment);
}
