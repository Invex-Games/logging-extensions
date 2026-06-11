namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     Persists formatted log entries to disk, handling file rollover and purging according to the active
///     <see cref="FileLoggerConfiguration" />.
/// </summary>
internal interface IFileLogWriter : IDisposable
{
    /// <summary>
    ///     Gets the time provider used for log timestamps and rollover decisions.
    /// </summary>
    internal TimeProvider TimeProvider { get; }

    /// <summary>
    ///     Performs any startup work required before entries can be logged (for example, starting a
    ///     background writer thread). Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    void Start();

    /// <summary>
    ///     Persists a single pre-formatted log entry.
    /// </summary>
    /// <param name="log">The fully formatted log entry, including the trailing newline.</param>
    /// <param name="logLevel">
    ///     The severity of the entry, used to resolve per-level file names via
    ///     <see cref="FileLoggerConfiguration.PerLevelLogName" />.
    /// </param>
    void Log(string log, LogLevel logLevel);
}
