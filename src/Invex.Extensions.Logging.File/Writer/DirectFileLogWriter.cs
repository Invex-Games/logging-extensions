namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     An <see cref="IFileLogWriter" /> that writes each log entry to disk synchronously on the calling
///     thread, flushing stream buffers before <see cref="Log" /> returns when writing succeeds.
/// </summary>
/// <param name="fileSystem">The file system abstraction used for all file operations.</param>
/// <param name="timeProvider">The time provider used for timestamps and rollover decisions.</param>
/// <param name="getCurrentConfig">
///     A delegate returning the current <see cref="FileLoggerConfiguration" />, evaluated on each write so
///     that runtime configuration changes take effect without a restart.
/// </param>
internal sealed class DirectFileLogWriter(
    IFileSystem fileSystem,
    TimeProvider timeProvider,
    Func<FileLoggerConfiguration> getCurrentConfig
) : IFileLogWriter
{
    /// <inheritdoc />
    public TimeProvider TimeProvider => timeProvider;

    /// <summary>
    ///     No startup work is required for direct writing; this method is a no-op.
    /// </summary>
    public void Start()
    {
        // No-op
    }

    /// <summary>
    ///     Resolves the captured group and severity, then writes the entry with rollover and retention.
    ///     Failures use one initial attempt and up to five retries with the same configuration snapshot,
    ///     then the entry is dropped. Stream flushing does not force a durable storage flush.
    /// </summary>
    /// <inheritdoc />
    public void Log(string log, LogLevel logLevel, string? group)
    {
        FileLoggerConfiguration? config = null;

        FileLogWriterUtil.TryWrite(() =>
        {
            #if NET8_0_OR_GREATER
            config ??= getCurrentConfig();
            #else
            config ??= getCurrentConfig()!;
            #endif

            var logName = FileLogWriterUtil.ResolveLogName(config, logLevel, group);

            FileLogWriterUtil.WriteLogEntries(fileSystem,
                timeProvider,
                config,
                logName,
                [log],
                Encoding.UTF8.GetByteCount(log));
        });
    }

    /// <summary>
    ///     No resources are held by the direct writer; this method is a no-op.
    /// </summary>
    public void Dispose()
    {
        // No-op
    }
}
