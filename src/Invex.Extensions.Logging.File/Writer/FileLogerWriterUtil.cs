namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     Shared helpers used by the file log writers for rolling over, purging, and appending to log files.
/// </summary>
internal static class FileLogWriterUtil
{
    /// <summary>
    ///     Appends the configured group and level suffixes to the standard base file name.
    /// </summary>
    /// <param name="config">The configuration snapshot for the current entry or batch.</param>
    /// <param name="logLevel">The severity of the entry.</param>
    /// <param name="group">The group captured when the entry was logged.</param>
    /// <returns>The standard base name followed by any nonempty group and level suffixes, in that order.</returns>
    public static string ResolveLogName(FileLoggerConfiguration config, LogLevel logLevel, string? group)
    {
        config.PerLevelLogName.TryGetValue(logLevel, out var levelSuffix);
        string? groupSuffix = null;

        if (group is { Length: > 0 })
            config.PerGroupLogName.TryGetValue(group, out groupSuffix);

        return ComposeLogName(config.LogName, groupSuffix, levelSuffix);
    }

    /// <summary>
    ///     Builds a destination name consistently for writing and reserving active files during rollover.
    /// </summary>
    /// <param name="logName">The standard base name, or null for the application's name.</param>
    /// <param name="groupSuffix">The group suffix; null or empty adds nothing.</param>
    /// <param name="levelSuffix">The level suffix; null or empty adds nothing.</param>
    /// <returns>The base name with an underscore before each nonempty suffix.</returns>
    private static string ComposeLogName(string? logName, string? groupSuffix, string? levelSuffix)
    {
        var name = logName ?? AppDomain.CurrentDomain.FriendlyName;

        if (!string.IsNullOrEmpty(groupSuffix))
            name += $"_{groupSuffix}";

        if (!string.IsNullOrEmpty(levelSuffix))
            name += $"_{levelSuffix}";

        return name;
    }

    /// <summary>
    ///     Executes a write operation with an initial attempt and up to five retries. Persistent failures
    ///     are reported to console/debug output and dropped without escaping into application code.
    /// </summary>
    /// <param name="operation">The operation to execute, including routing and path resolution.</param>
    /// <returns>Whether the operation succeeded.</returns>
    public static bool TryWrite(Action operation)
    {
        for (var attempt = 0; attempt <= 5; attempt++)
        {
            try
            {
                operation();

                return true;
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
                    // Reporting a logging failure must not cause another failure.
                }
            }
        }

        return false;
    }

    /// <summary>
    ///     Writes entries to a resolved route, applying the same directory creation, rollover, retention,
    ///     and creation timestamps in buffered and direct modes. The caller supplies retry handling.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used for all file operations.</param>
    /// <param name="timeProvider">The time provider used for rollover and file timestamps.</param>
    /// <param name="config">The configuration snapshot for the current entry or batch.</param>
    /// <param name="logName">The resolved base file name.</param>
    /// <param name="logs">The formatted entries, including trailing newlines.</param>
    /// <param name="logsLengthBytes">The combined UTF-8 length of the entries.</param>
    public static void WriteLogEntries(
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        FileLoggerConfiguration config,
        string logName,
        IEnumerable<string> logs,
        int logsLengthBytes)
    {
        var logsDirectory = fileSystem.Path.IsPathRooted(config.LogDirectory)
            ? config.LogDirectory
            : fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), config.LogDirectory);

        if (!fileSystem.Directory.Exists(logsDirectory))
            fileSystem.Directory.CreateDirectory(logsDirectory);

        var logFilePath = fileSystem.Path.GetFullPath(fileSystem.Path.Combine(logsDirectory, $"{logName}.log"));
        var routeDirectory = fileSystem.Path.GetDirectoryName(logFilePath)!;
        var routeName = fileSystem.Path.GetFileNameWithoutExtension(logFilePath);
        var fileInfo = fileSystem.FileInfo.New(logFilePath);
        var newFileCreated = !fileInfo.Exists;
        HashSet<string>? activeLogNames = null;

        if (!newFileCreated && fileInfo.Length + logsLengthBytes >= config.FileSizeLimitBytes)
        {
            activeLogNames = GetActiveLogPaths(fileSystem, config, logsDirectory);
            RollOnFileSize(fileSystem, timeProvider, routeDirectory, routeName, logFilePath, activeLogNames);
            newFileCreated = true;
        }

        if (!newFileCreated && config.RolloverInterval is not FileRolloverInterval.Infinite)
            newFileCreated = RollOnTimeInterval(fileSystem,
                timeProvider,
                config.RolloverInterval,
                fileInfo,
                routeDirectory,
                routeName,
                logFilePath,
                // ReSharper disable once AccessToModifiedClosure -- activeLogNames is only assigned if the rollover interval has elapsed, so it is safe to resolve it lazily here.
                () => activeLogNames ??= GetActiveLogPaths(fileSystem, config, logsDirectory));

        if (newFileCreated)
            PurgeOnTotalSize(fileSystem,
                config.MaxTotalSizeBytes,
                routeDirectory,
                routeName,
                activeLogNames ??= GetActiveLogPaths(fileSystem, config, logsDirectory));

        WriteToFile(fileSystem, logFilePath, logs);

        if (!newFileCreated)
            return;

        fileInfo.Refresh();

        fileInfo.CreationTimeUtc = fileInfo.LastWriteTimeUtc = fileInfo.LastAccessTimeUtc = timeProvider.GetUtcNow()
            .DateTime;
    }

    /// <summary>
    ///     Enumerates normalized active file paths so that retention and archive naming cannot claim
    ///     another route's file, even when that file has not yet been created.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction providing the platform's path separator.</param>
    /// <param name="config">The configuration snapshot for the current entry or batch.</param>
    /// <param name="logsDirectory">The resolved directory containing the log files.</param>
    /// <returns>The normalized active paths, compared using the platform's usual filename casing rules.</returns>
    private static HashSet<string> GetActiveLogPaths(
        IFileSystem fileSystem,
        FileLoggerConfiguration config,
        string logsDirectory)
    {
        var names = new HashSet<string>(fileSystem.Path.DirectorySeparatorChar == '\\'
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

        AddActiveLogPath(fileSystem, logsDirectory, names, ComposeLogName(config.LogName, null, null));

        foreach (var levelSuffix in config.PerLevelLogName.Values)
            AddActiveLogPath(fileSystem, logsDirectory, names, ComposeLogName(config.LogName, null, levelSuffix));

        foreach (var groupSuffix in config.PerGroupLogName.Values)
        {
            AddActiveLogPath(fileSystem, logsDirectory, names, ComposeLogName(config.LogName, groupSuffix, null));

            foreach (var levelSuffix in config.PerLevelLogName.Values)
                AddActiveLogPath(fileSystem,
                    logsDirectory,
                    names,
                    ComposeLogName(config.LogName, groupSuffix, levelSuffix));
        }

        return names;
    }

    /// <summary>
    ///     Reserves a normalized active file path. An invalid configured destination cannot collide with
    ///     a valid archive and must not prevent healthy routes from creating or rolling their files.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used for path normalization.</param>
    /// <param name="logsDirectory">The resolved directory containing the log files.</param>
    /// <param name="activePaths">The active paths to protect.</param>
    /// <param name="logName">A configured base name, or null for the application name.</param>
    private static void AddActiveLogPath(
        IFileSystem fileSystem,
        string logsDirectory,
        HashSet<string> activePaths,
        string? logName)
    {
        try
        {
            var path = fileSystem.Path.Combine(logsDirectory, $"{logName ?? AppDomain.CurrentDomain.FriendlyName}.log");
            activePaths.Add(fileSystem.Path.GetFullPath(path));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // The selected route still reports and retries invalid-path failures when it is written.
        }
    }

    /// <summary>
    ///     Rolls over the active log file because it has reached the configured size limit. The file is
    ///     renamed to <c>{logName}_{yyMMdd-HHmmss}.log</c> (with a numeric <c>_{n}</c> suffix appended if
    ///     that name already exists or is reserved for an active route), allowing a new active file to be
    ///     created on the next write.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used for the rename.</param>
    /// <param name="timeProvider">The time provider used to timestamp the rolled-over file name.</param>
    /// <param name="logsDirectory">The directory containing the log files.</param>
    /// <param name="logName">The base log file name, without extension.</param>
    /// <param name="logFilePath">The full path of the active log file to roll over.</param>
    /// <param name="activeLogNames">Normalized active file paths that cannot be used as archive destinations.</param>
    private static void RollOnFileSize(
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        string logsDirectory,
        string logName,
        string logFilePath,
        ISet<string>? activeLogNames)
    {
        string newLogFilePath;

        for (var i = 0;; i++)
        {
            var suffix = i == 0
                ? string.Empty
                : $"_{i}";

            var archiveName = $"{logName}_{timeProvider.GetLocalNow():yyMMdd-HHmmss}{suffix}";

            newLogFilePath = fileSystem.Path.Combine(logsDirectory, $"{archiveName}.log");

            if (activeLogNames?.Contains(fileSystem.Path.GetFullPath(newLogFilePath)) is true)
                continue;

            if (!fileSystem.File.Exists(newLogFilePath))
                break;
        }

        fileSystem.File.Move(logFilePath, newLogFilePath);
    }

    /// <summary>
    ///     Rolls over the active log file if the time elapsed since its creation meets or exceeds the
    ///     configured <paramref name="rolloverInterval" />. The file is renamed using the same
    ///     <c>{logName}_{yyMMdd-HHmmss}.log</c> scheme as <see cref="RollOnFileSize" />.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used for the rename.</param>
    /// <param name="timeProvider">The time provider used to evaluate elapsed time and timestamp the file name.</param>
    /// <param name="rolloverInterval">The configured rollover interval.</param>
    /// <param name="fileInfo">The file info of the active log file, used to read its creation time.</param>
    /// <param name="logsDirectory">The directory containing the log files.</param>
    /// <param name="logName">The base log file name, without extension.</param>
    /// <param name="logFilePath">The full path of the active log file to roll over.</param>
    /// <param name="getActiveLogNames">Resolves normalized active file paths only when rollover is due.</param>
    /// <returns>
    ///     <see langword="true" /> if the file was rolled over; <see langword="false" /> if the interval has
    ///     not yet elapsed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="rolloverInterval" /> is not a defined <see cref="FileRolloverInterval" /> value.
    /// </exception>
    private static bool RollOnTimeInterval(
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        FileRolloverInterval rolloverInterval,
        IFileInfo fileInfo,
        string logsDirectory,
        string logName,
        string logFilePath,
        Func<ISet<string>> getActiveLogNames)
    {
        var now = timeProvider.GetLocalNow();

        var fileCreatedAt =
            ((DateTimeOffset)fileInfo.CreationTimeUtc).ToOffset(timeProvider.LocalTimeZone.BaseUtcOffset);

        var rollingTimeSpan = rolloverInterval switch
        {
            FileRolloverInterval.Year => TimeSpan.FromDays(365),
            FileRolloverInterval.Month => TimeSpan.FromDays(30),
            FileRolloverInterval.Day => TimeSpan.FromDays(1),
            FileRolloverInterval.Hour => TimeSpan.FromHours(1),
            FileRolloverInterval.Minute => TimeSpan.FromMinutes(1),
            FileRolloverInterval.Infinite => TimeSpan.MaxValue,
            _ => throw new ArgumentOutOfRangeException(nameof(rolloverInterval),
                rolloverInterval,
                "Invalid rolling interval"),
        };

        if (now - fileCreatedAt < rollingTimeSpan)
            return false;

        RollOnFileSize(fileSystem, timeProvider, logsDirectory, logName, logFilePath, getActiveLogNames());

        return true;
    }

    /// <summary>
    ///     Deletes the oldest archive of the exact base name if their combined size meets or exceeds
    ///     <paramref name="maxTotalSizeBytes" />. Only timestamped archive names with an optional numeric
    ///     collision suffix qualify; configured active names are excluded. At most one file is deleted.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used to enumerate and delete files.</param>
    /// <param name="maxTotalSizeBytes">The maximum combined size, in bytes, of rolled-over log files.</param>
    /// <param name="logsDirectory">The directory containing the log files.</param>
    /// <param name="logName">The base log file name, without extension.</param>
    /// <param name="activeLogNames">Normalized active file paths that must never be purged.</param>
    private static void PurgeOnTotalSize(
        IFileSystem fileSystem,
        long maxTotalSizeBytes,
        string logsDirectory,
        string logName,
        ISet<string>? activeLogNames)
    {
        var prefix = $"{logName}_";

        var comparison = fileSystem.Path.DirectorySeparatorChar == '\\'
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        var allLogs = fileSystem
            .Directory
            .GetFiles(logsDirectory, "*.log")
            .Where(file =>
            {
                var name = fileSystem.Path.GetFileNameWithoutExtension(file);

                return name.StartsWith(prefix, comparison) &&
                       activeLogNames?.Contains(fileSystem.Path.GetFullPath(file)) is not true &&
                       Regex.IsMatch(name[prefix.Length..], @"\A[0-9]{6}-[0-9]{6}(?:_[1-9][0-9]*)?\z");
            })
            .ToArray();

        if (allLogs.Length == 0)
            return;

        var totalSize = allLogs.Sum(file => fileSystem.FileInfo.New(file)
            .Length);

        if (totalSize < maxTotalSizeBytes)
            return;

        var oldestLog = allLogs
            .OrderBy(file => fileSystem.FileInfo.New(file)
                .CreationTime)
            .First();

        fileSystem.File.Delete(oldestLog);
    }

    /// <summary>
    ///     Appends the given pre-formatted log entries to the file at <paramref name="filePath" />, creating
    ///     the file if it does not exist, and flushes the stream before returning.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used to open the file.</param>
    /// <param name="filePath">The full path of the log file to append to.</param>
    /// <param name="logs">The log entries to write; each entry is expected to include its trailing newline.</param>
    private static void WriteToFile(IFileSystem fileSystem, string filePath, IEnumerable<string> logs)
    {
        using var writer = fileSystem.File.AppendText(filePath);

        foreach (var log in logs)
            writer.Write(log);

        writer.Flush();
    }
}
