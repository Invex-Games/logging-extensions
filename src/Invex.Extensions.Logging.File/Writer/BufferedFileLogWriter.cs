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
///     The background thread reads up to 10 entries per iteration and groups them by resolved file name
///     and severity. Group identity is captured when logging; file names are resolved when writing.
///     Disposal drains all remaining queued entries before the background thread exits.
/// </remarks>
internal sealed class BufferedFileLogWriter(
    IFileSystem fileSystem,
    TimeProvider timeProvider,
    Func<FileLoggerConfiguration> getCurrentConfig
) : IFileLogWriter
{
    /// <summary>
    ///     The queue shared by logging threads and the single background writer.
    /// </summary>
    private readonly Channel<LogEvent> _logEntryChannel = Channel.CreateUnbounded<LogEvent>(new()
    {
        SingleReader = true,
        SingleWriter = false,
    });

    /// <summary>
    ///     The background writer thread, created when the provider starts.
    /// </summary>
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
            getCurrentConfig));

        _writerThread.Start();
    }

    /// <summary>
    ///     Enqueues the entry and its captured group. Enqueue failures are reported and retried up to
    ///     five times before the entry is dropped.
    /// </summary>
    /// <inheritdoc />
    public void Log(string log, LogLevel logLevel, string? group) =>
        FileLogWriterUtil.TryWrite(() => _logEntryChannel.Writer.TryWrite(new(log, logLevel)
        {
            Group = group,
        }));

    /// <summary>
    ///     Signals the background thread to stop and blocks until it has drained all remaining queued
    ///     entries to disk and exited. Entries logged after disposal are dropped.
    /// </summary>
    public void Dispose()
    {
        _logEntryChannel.Writer.TryComplete();
        _writerThread?.Join();
        _writerThread = null;
    }

    /// <summary>
    ///     Drains up to 10 entries at a time and writes each resolved file/severity bucket. Routing and
    ///     file-operation failures are retried without terminating the background worker.
    /// </summary>
    /// <param name="reader">The channel reader to drain log entries from.</param>
    /// <param name="fileSystem">The file system abstraction used for all file operations.</param>
    /// <param name="timeProvider">The time provider used for timestamps and rollover decisions.</param>
    /// <param name="getCurrentConfig">A delegate returning the current configuration.</param>
    private static void RunBackgroundThread(
        ChannelReader<LogEvent> reader,
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        Func<FileLoggerConfiguration> getCurrentConfig)
    {
        // Channel completion ends the wait only after all queued entries have been read. Checking
        // a separate stop signal after an empty read could miss an entry enqueued between the two.
        while (reader
               .WaitToReadAsync()
               .AsTask()
               .GetAwaiter()
               .GetResult())
        {
            var entries = new List<LogEvent>();
            const int maxReadCount = 10;

            while (entries.Count < maxReadCount && reader.TryRead(out var item))
                entries.Add(item);

            FileLoggerConfiguration? config = null;
            LogRouteComparer? routeComparer = null;

            if (!FileLogWriterUtil.TryWrite(() =>
                {
                    config = getCurrentConfig();

                    routeComparer = new(fileSystem.Path.DirectorySeparatorChar == '\\'
                        ? StringComparer.OrdinalIgnoreCase
                        : StringComparer.Ordinal);
                }))
                continue;

            var logsByRoute =
                new Dictionary<(string FilePath, LogLevel Level), (string LogName, List<string> Logs)>(routeComparer);

            var lengthsByRoute = new Dictionary<(string FilePath, LogLevel Level), int>(routeComparer);

            foreach (var entry in entries)
                FileLogWriterUtil.TryWrite(() =>
                {
                    var logName = FileLogWriterUtil.ResolveLogName(config!, entry.LogLevel, entry.Group);

                    var filePath =
                        fileSystem.Path.GetFullPath(fileSystem.Path.Combine(config!.LogDirectory, $"{logName}.log"));

                    var route = (filePath, entry.LogLevel);
                    logsByRoute.TryAdd(route, (logName, []));

                    logsByRoute[route]
                        .Logs
                        .Add(entry.Message);

                    lengthsByRoute.TryAdd(route, 0);
                    lengthsByRoute[route] += Encoding.UTF8.GetByteCount(entry.Message);
                });

            foreach (var route in logsByRoute.Keys)
                FileLogWriterUtil.TryWrite(() => FileLogWriterUtil.WriteLogEntries(fileSystem,
                    timeProvider,
                    config!,
                    logsByRoute[route].LogName,
                    logsByRoute[route].Logs,
                    lengthsByRoute[route]));
        }
    }

    /// <summary>
    ///     Compares batch routes using the platform's usual filename casing rules and exact severity,
    ///     preserving entry order when normalized configured destinations select the same file.
    /// </summary>
    /// <param name="logNameComparer">The filename comparer for the writer's file system.</param>
    private sealed class LogRouteComparer(StringComparer logNameComparer)
        : IEqualityComparer<(string FilePath, LogLevel Level)>
    {
        /// <inheritdoc />
        public bool Equals((string FilePath, LogLevel Level) x, (string FilePath, LogLevel Level) y) =>
            x.Level == y.Level && logNameComparer.Equals(x.FilePath, y.FilePath);

        /// <inheritdoc />
        public int GetHashCode((string FilePath, LogLevel Level) obj) =>
            unchecked((logNameComparer.GetHashCode(obj.FilePath) * 397) ^ (int)obj.Level);
    }

    /// <summary>
    ///     A queued log entry containing its formatted message, severity, and captured scope group.
    /// </summary>
    /// <param name="Message">The fully formatted log entry, including the trailing newline.</param>
    /// <param name="LogLevel">The severity of the entry, used to resolve per-level file names.</param>
    private sealed record LogEvent(string Message, LogLevel LogLevel)
    {
        /// <summary>
        ///     Gets the scope group captured on the logging thread.
        /// </summary>
        public string? Group { get; init; }
    }
}
