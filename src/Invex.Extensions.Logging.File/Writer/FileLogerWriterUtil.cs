namespace Invex.Extensions.Logging.File.Writer;

/// <summary>
///     Shared helpers used by the file log writers for rolling over, purging, and appending to log files.
/// </summary>
internal static class FileLogWriterUtil
{
    /// <summary>
    ///     Rolls over the active log file because it has reached the configured size limit. The file is
    ///     renamed to <c>{logName}_{yyMMdd-HHmmss}.log</c> (with a numeric <c>_{n}</c> suffix appended if
    ///     that name is already taken), allowing a new active file to be created on the next write.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used for the rename.</param>
    /// <param name="timeProvider">The time provider used to timestamp the rolled-over file name.</param>
    /// <param name="logsDirectory">The directory containing the log files.</param>
    /// <param name="logName">The base log file name, without extension.</param>
    /// <param name="logFilePath">The full path of the active log file to roll over.</param>
    public static void RollOnFileSize(
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        string logsDirectory,
        string logName,
        string logFilePath)
    {
        string newLogFilePath;

        for (var i = 0;; i++)
        {
            var suffix = i == 0
                ? string.Empty
                : $"_{i}";

            newLogFilePath = fileSystem.Path.Combine(logsDirectory,
                $"{logName}_{timeProvider.GetLocalNow():yyMMdd-HHmmss}{suffix}.log");

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
    /// <returns>
    ///     <see langword="true" /> if the file was rolled over; <see langword="false" /> if the interval has
    ///     not yet elapsed.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="rolloverInterval" /> is not a defined <see cref="FileRolloverInterval" /> value.
    /// </exception>
    public static bool RollOnTimeInterval(
        IFileSystem fileSystem,
        TimeProvider timeProvider,
        FileRolloverInterval rolloverInterval,
        IFileInfo fileInfo,
        string logsDirectory,
        string logName,
        string logFilePath)
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

        string newLogFilePath;

        for (var i = 0;; i++)
        {
            var suffix = i == 0
                ? string.Empty
                : $"_{i}";

            newLogFilePath = fileSystem.Path.Combine(logsDirectory,
                $"{logName}_{timeProvider.GetLocalNow():yyMMdd-HHmmss}{suffix}.log");

            if (!fileSystem.File.Exists(newLogFilePath))
                break;
        }

        fileSystem.File.Move(logFilePath, newLogFilePath);

        return true;
    }

    /// <summary>
    ///     Deletes the oldest rolled-over log file (matching <c>{logName}_*.log</c>) if the combined size of
    ///     all rolled-over files meets or exceeds <paramref name="maxTotalSizeBytes" />. At most one file is
    ///     deleted per call; this is invoked on each rollover, keeping total disk usage bounded over time.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction used to enumerate and delete files.</param>
    /// <param name="maxTotalSizeBytes">The maximum combined size, in bytes, of rolled-over log files.</param>
    /// <param name="logsDirectory">The directory containing the log files.</param>
    /// <param name="logName">The base log file name, without extension.</param>
    public static void PurgeOnTotalSize(
        IFileSystem fileSystem,
        long maxTotalSizeBytes,
        string logsDirectory,
        string logName)
    {
        var allLogs = fileSystem.Directory.GetFiles(logsDirectory, $"{logName}_*.log");

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
    public static void WriteToFile(IFileSystem fileSystem, string filePath, IEnumerable<string> logs)
    {
        using var writer = fileSystem.File.AppendText(filePath);

        foreach (var log in logs)
            writer.Write(log);

        writer.Flush();
    }
}
