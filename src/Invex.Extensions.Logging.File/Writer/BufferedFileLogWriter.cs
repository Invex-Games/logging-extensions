namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     An <see cref="IFileLogWriter" /> that enqueues log entries onto an unbounded in-memory channel and
///     drains them to disk on a dedicated background thread, keeping file I/O off application threads.
/// </summary>
/// <param name="fileSystem">The file system abstraction used for all file operations.</param>
/// <param name="timeProvider">The time provider used for timestamps and rollover decisions.</param>
/// <param name="getCurrentConfig">
///     A delegate returning the current <see cref="FileLoggerConfiguration" />, evaluated on each batch so
///     that runtime configuration changes take effect without a restart.
/// </param>
/// <remarks>
///     The background thread reads entries in batches (up to 10 per iteration), groups them by
///     <see cref="LogLevel" /> to resolve the target file name, and applies size-based rollover, time-based
///     rollover, and total-size purging before appending. Disposal signals the background thread to stop and
///     blocks until it has drained all remaining queued entries to disk and exited.
/// </remarks>
internal sealed class BufferedFileLogWriter(
    IFileSystem fileSystem,
    TimeProvider timeProvider,
    Func<FileLoggerConfiguration> getCurrentConfig
) : IFileLogWriter
{
    private readonly Channel<LogEvent> _logEntryChannel = Channel.CreateUnbounded<LogEvent>(new()
    {
        SingleReader = true,
        SingleWriter = false,
    });

    private readonly CancellationTokenSource _writerCancelTokenSource = new();
    private Thread? _writerThread;

    /// <inheritdoc />
    public TimeProvider TimeProvider => timeProvider;

    /// <summary>
    ///     Starts the background writer thread. Safe to call multiple times; subsequent calls are no-ops.
    /// </summary>
    public void Start()
    {
        if (_writerThread is not null)
            return;

        _writerThread = new(() => RunBackgroundThread(_logEntryChannel.Reader,
            fileSystem,
            TimeProvider,
            getCurrentConfig,
            _writerCancelTokenSource.Token));

        _writerThread.Start();
    }

    /// <summary>
    ///     Enqueues a log entry for the background thread to write. Retries up to five times on failure
    ///     before rethrowing.
    /// </summary>
    /// <inheritdoc />
    public void Log(string log, LogLevel logLevel)
    {
        var attempt = 0;

        while (true)
        {
            try
            {
                _logEntryChannel.Writer.TryWrite(new(log, logLevel));

                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);

                if (attempt >= 5)
                    throw;

                attempt++;
            }
        }
    }

    /// <summary>
    ///     Signals the background thread to stop and blocks until it has drained all remaining queued
    ///     entries to disk and exited. Entries logged after disposal are dropped.
    /// </summary>
    public void Dispose()
    {
        _logEntryChannel.Writer.TryComplete();
        _writerCancelTokenSource.Cancel();
        _writerThread?.Join();
        _writerThread = null;
    }

    /// <summary>
    ///     The background thread's main loop: repeatedly drains up to 10 entries from the channel, groups
    ///     them by <see cref="LogLevel" />, and writes each group to its target file, performing rollover
    ///     and purging as needed. Per-group write failures are logged to the console/debug output and
    ///     retried up to five times before the group is skipped.
    /// </summary>
    /// <param name="reader">The channel reader to drain log entries from.</param>
    /// <param name="fileSystem">The file system abstraction used for all file operations.</param>
    /// <param name="timeProvider">The time provider used for timestamps and rollover decisions.</param>
    /// <param name="getCurrentConfig">A delegate returning the current configuration.</param>
    /// <param name="cancellationToken">
    ///     A token that stops the loop once cancelled and the channel has been fully drained.
    /// </param>
    private static void RunBackgroundThread(
        ChannelReader<LogEvent> reader,
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        Func<FileLoggerConfiguration> getCurrentConfig,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            var logsByLevel = new Dictionary<LogLevel, List<string>>();
            var logsLengthBytesByLevel = new Dictionary<LogLevel, int>();

            #if NET8_0_OR_GREATER
            var config = getCurrentConfig();
            #else
            var config = getCurrentConfig()!;
            #endif

            var readCount = 0;
            const int maxReadCount = 10;

            while (readCount < maxReadCount && reader.TryRead(out var item))
            {
                logsByLevel.TryAdd(item.LogLevel, []);

                logsByLevel[item.LogLevel]
                    .Add(item.Message);

                logsLengthBytesByLevel.TryAdd(item.LogLevel, 0);
                logsLengthBytesByLevel[item.LogLevel] += Encoding.UTF8.GetByteCount(item.Message);

                readCount++;
            }

            if (logsByLevel.Count == 0)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                continue;
            }

            foreach (var logLevel in logsByLevel.Keys)
            {
                var logs = logsByLevel[logLevel];
                var logsLengthBytes = logsLengthBytesByLevel[logLevel];

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

                var attempt = 0;

                while (true)
                {
                    try
                    {
                        var fileInfo = fileSystem.FileInfo.New(logFilePath);
                        var newFileCreated = !fileInfo.Exists;

                        if (!newFileCreated && fileInfo.Length + logsLengthBytes >= config.FileSizeLimitBytes)
                        {
                            FileLogWriterUtil.RollOnFileSize(fileSystem,
                                timeProvider,
                                logsDirectory,
                                logName,
                                logFilePath);

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
                            FileLogWriterUtil.PurgeOnTotalSize(fileSystem,
                                config.MaxTotalSizeBytes,
                                logsDirectory,
                                logName);

                        FileLogWriterUtil.WriteToFile(fileSystem, logFilePath, logs);

                        // If we have rolled over the file or are writing for the first time, we want to ensure the
                        // file has the correct timestamps
                        if (newFileCreated)
                        {
                            fileInfo.Refresh();

                            fileInfo.CreationTimeUtc = fileInfo.LastWriteTimeUtc = fileInfo.LastAccessTimeUtc =
                                timeProvider.GetUtcNow()
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
        }
    }

    /// <summary>
    ///     A queued log entry pairing the formatted message with its severity.
    /// </summary>
    /// <param name="Message">The fully formatted log entry, including the trailing newline.</param>
    /// <param name="LogLevel">The severity of the entry, used to resolve per-level file names.</param>
    private sealed record LogEvent(string Message, LogLevel LogLevel);
}
