namespace Invex.Extensions.Logging.File.Configuration;

/// <summary>
///     Configuration options for the file logger. Bound from the <c>Logging:File</c> configuration section
///     when registered via
///     <see cref="FileLoggerExtension.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, bool)" />,
///     and can also be set programmatically via the
///     <see cref="FileLoggerExtension.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, System.Action{FileLoggerConfiguration}, bool)" />
///     overload.
/// </summary>
/// <remarks>
///     Changes made to the bound configuration at runtime (for example, by editing <c>appsettings.json</c>)
///     are picked up automatically and applied to subsequent log writes.
/// </remarks>
[PublicAPI]
public sealed class FileLoggerConfiguration
{
    /// <summary>
    ///     The default value of <see cref="LogDirectory" />: <c>"Logs"</c>.
    /// </summary>
    public const string DefaultLogDirectory = "Logs";

    /// <summary>
    ///     The default value of <see cref="LogName" />: <see langword="null" />, which means the
    ///     current application's name (<see cref="System.AppDomain.FriendlyName" />) is used.
    /// </summary>
    public const string? DefaultLogName = null;

    /// <summary>
    ///     The default value of <see cref="FileSizeLimitBytes" />: 100 MiB.
    /// </summary>
    public const long DefaultFileSizeLimitBytes = 100L * 1024 * 1024;

    /// <summary>
    ///     The default value of <see cref="RolloverInterval" />: <see cref="FileRolloverInterval.Day" />.
    /// </summary>
    public const FileRolloverInterval DefaultRollingInterval = FileRolloverInterval.Day;

    /// <summary>
    ///     The default value of <see cref="MaxTotalSizeBytes" />: 10 GiB.
    /// </summary>
    public const long DefaultMaxTotalSizeBytes = 10L * 1024 * 1024 * 1024;

    /// <summary>
    ///     Gets or sets the directory where log files are written. May be an absolute path, or a path
    ///     relative to the application's current working directory. The directory is created automatically
    ///     if it does not exist. Defaults to <see cref="DefaultLogDirectory" />.
    /// </summary>
    public string LogDirectory { get; set; } = DefaultLogDirectory;

    /// <summary>
    ///     Gets or sets the base file name (without extension) of the log file. The active log file is named
    ///     <c>{LogName}.log</c>, and rolled-over files are named <c>{LogName}_{timestamp}.log</c>.
    ///     When <see langword="null" /> (the default), the current application's name
    ///     (<see cref="System.AppDomain.FriendlyName" />) is used.
    /// </summary>
    public string? LogName { get; set; } = DefaultLogName;

    /// <summary>
    ///     Gets or sets per-<see cref="LogLevel" /> overrides of the log file name, allowing entries of specific
    ///     levels to be routed to separate files. For levels present in this dictionary, the mapped name is used
    ///     in place of <see cref="LogName" />; a <see langword="null" /> value falls back to the application's
    ///     name. Levels not present use <see cref="LogName" />. Empty by default.
    /// </summary>
    public Dictionary<LogLevel, string?> PerLevelLogName { get; set; } = [];

    /// <summary>
    ///     Gets or sets the maximum size, in bytes, of a single log file. When writing an entry would cause the
    ///     active file to reach this limit, the file is rolled over (renamed with a timestamp suffix) and a new
    ///     active file is started. Defaults to <see cref="DefaultFileSizeLimitBytes" /> (100 MiB).
    /// </summary>
    public long FileSizeLimitBytes { get; set; } = DefaultFileSizeLimitBytes;

    /// <summary>
    ///     Gets or sets the time interval after which the active log file is rolled over, based on the file's
    ///     creation time. Use <see cref="FileRolloverInterval.Infinite" /> to disable time-based rollover.
    ///     Defaults to <see cref="DefaultRollingInterval" /> (<see cref="FileRolloverInterval.Day" />).
    /// </summary>
    public FileRolloverInterval RolloverInterval { get; set; } = DefaultRollingInterval;

    /// <summary>
    ///     Gets or sets the maximum combined size, in bytes, of rolled-over log files sharing the same base name.
    ///     When a rollover occurs and the total size of rolled-over files meets or exceeds this limit, the oldest
    ///     rolled-over file is deleted. Defaults to <see cref="DefaultMaxTotalSizeBytes" /> (10 GiB).
    /// </summary>
    public long MaxTotalSizeBytes { get; set; } = DefaultMaxTotalSizeBytes;
}
