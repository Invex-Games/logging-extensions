namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     An <see cref="IFileLogWriter" /> that writes each log entry to disk synchronously on the calling
///     thread, guaranteeing the entry is persisted before <see cref="Log" /> returns.
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
    ///     Writes the entry to its target file, performing size-based rollover, time-based rollover, and
    ///     total-size purging as needed. Failures are logged to the console/debug output and retried up to
    ///     five times before the entry is dropped.
    /// </summary>
    /// <inheritdoc />
    public void Log(string log, LogLevel logLevel)
    {
        #if NET8_0_OR_GREATER
        var config = getCurrentConfig();
        #else
        var config = getCurrentConfig()!;
        #endif

        var logLengthBytes = Encoding.UTF8.GetByteCount(log);

        var attempt = 0;

        while (true)
        {
            try
            {
                var logsDirectoryName = config.LogDirectory;

                var logsDirectory = fileSystem.Path.IsPathRooted(logsDirectoryName)
                    ? logsDirectoryName
                    : fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), logsDirectoryName);

                if (!fileSystem.Directory.Exists(logsDirectory))
                    fileSystem.Directory.CreateDirectory(logsDirectory);

                var logName = config.PerLevelLogName.TryGetValue(logLevel, out var name)
                    ? name ?? AppDomain.CurrentDomain.FriendlyName
                    : config.LogName ?? AppDomain.CurrentDomain.FriendlyName;

                var logFilePath = fileSystem.Path.Combine(logsDirectory, $"{logName}.log");

                var fileInfo = fileSystem.FileInfo.New(logFilePath);
                var newFileCreated = !fileInfo.Exists;

                if (!newFileCreated && fileInfo.Length + logLengthBytes >= config.FileSizeLimitBytes)
                {
                    FileLogWriterUtil.RollOnFileSize(fileSystem, timeProvider, logsDirectory, logName, logFilePath);
                    newFileCreated = true;
                }

                if (!newFileCreated && config.RolloverInterval is not FileRolloverInterval.Infinite)
                    newFileCreated = FileLogWriterUtil.RollOnTimeInterval(fileSystem,
                        timeProvider,
                        config.RolloverInterval,
                        fileInfo,
                        logsDirectory,
                        logName,
                        logFilePath);

                if (newFileCreated)
                    FileLogWriterUtil.PurgeOnTotalSize(fileSystem, config.MaxTotalSizeBytes, logsDirectory, logName);

                FileLogWriterUtil.WriteToFile(fileSystem, logFilePath, [log]);

                // If we have rolled over the file or are writing for the first time, we want to ensure the
                // file has the correct timestamps
                if (newFileCreated)
                {
                    fileInfo.Refresh();

                    fileInfo.CreationTimeUtc = fileInfo.LastWriteTimeUtc = fileInfo.LastAccessTimeUtc = timeProvider
                        .GetUtcNow()
                        .DateTime;
                }

                break;
            }
            catch (Exception ex)
            {
                try
                {
                    Console.WriteLine(ex);
                    Debug.WriteLine(ex);
                }
                catch
                {
                    // Can't do anything more here, better to just continue
                }

                if (attempt >= 5)
                    break;

                attempt++;
            }
        }
    }

    /// <summary>
    ///     No resources are held by the direct writer; this method is a no-op.
    /// </summary>
    public void Dispose()
    {
        // No-op
    }
}
