namespace Invex.Extensions.Logging.File.Configuration;

/// <summary>
///     Configuration options for the file logger. Bound from the <c>Logging:File</c> configuration section
///     when registered via
///     <see cref="FileLoggerExtension.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, bool)" />,
///     and can also be set programmatically via the
///     <see
///         cref="FileLoggerExtension.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, System.Action{FileLoggerConfiguration}, bool)" />
///     overload.
/// </summary>
/// <remarks>
///     Reloadable configuration sources update these options through the options monitor. Writers use
///     the current options for each entry or buffered batch, so already queued entries can use new routes.
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
    ///     <c>{LogName}.log</c> before any configured group and level suffixes are appended. Rolled-over files
    ///     append <c>_{timestamp}</c> to the complete name before the extension.
    ///     When <see langword="null" /> (the default), the current application's name
    ///     (<see cref="System.AppDomain.FriendlyName" />) is used. An empty string is used literally.
    /// </summary>
    /// <remarks>
    ///     Names and mapped suffixes are used verbatim. Use file name components without an extension
    ///     or directory separators, and set the destination directory through <see cref="LogDirectory" />.
    /// </remarks>
    public string? LogName { get; set; } = DefaultLogName;

    /// <summary>
    ///     Gets or sets per-<see cref="LogLevel" /> file name suffixes. A matching nonempty suffix is appended
    ///     to <see cref="LogName" /> (or the application's name when null), separated by an underscore.
    ///     When a group also matches <see cref="PerGroupLogName" />, its suffix precedes the level suffix:
    ///     <c>{LogName}_{groupSuffix}_{levelSuffix}.log</c>. Missing levels and mapped null or empty values
    ///     add no level suffix or separator. Empty by default.
    /// </summary>
    public Dictionary<LogLevel, string?> PerLevelLogName { get; set; } = [];

    /// <summary>
    ///     Gets or sets file name suffixes for groups supplied by a logging scope's <c>Group</c> property.
    ///     A matching nonempty suffix is appended to <see cref="LogName" /> (or the application's name when
    ///     null), separated by an underscore, before any <see cref="PerLevelLogName" /> suffix. Missing or
    ///     unmapped groups and mapped null or empty values add no group suffix or separator. Empty by default,
    ///     with case-sensitive group matching; a replacement dictionary's comparer is respected.
    /// </summary>
    /// <remarks>
    ///     The innermost nonempty string <c>Group</c> value wins. An unmapped inner group suppresses an
    ///     outer group's mapping. Null, empty, and non-string scope values leave the outer group effective.
    ///     The group is captured when logging; its mapping is resolved when the entry is written.
    /// </remarks>
    public Dictionary<string, string?> PerGroupLogName { get; set; } = [];

    /// <summary>
    ///     Gets or sets the size-based rollover threshold, in bytes. An existing active file is rolled over
    ///     before its size plus the pending UTF-8 entry or batch size would meet or exceed this value.
    ///     Defaults to <see cref="DefaultFileSizeLimitBytes" /> (100 MiB).
    /// </summary>
    /// <remarks>
    ///     Entries and batches are not split, so a new active file can exceed this threshold. Zero or negative
    ///     values cause every write to an existing active file to roll over; they do not disable rollover.
    /// </remarks>
    public long FileSizeLimitBytes { get; set; } = DefaultFileSizeLimitBytes;

    /// <summary>
    ///     Gets or sets the time interval after which the active log file is rolled over, based on the file's
    ///     creation time. Use <see cref="FileRolloverInterval.Infinite" /> to disable time-based rollover.
    ///     Defaults to <see cref="DefaultRollingInterval" /> (<see cref="FileRolloverInterval.Day" />).
    /// </summary>
    /// <remarks>
    ///     Checked only before writes. Intervals measure elapsed time rather than calendar boundaries;
    ///     a month is 30 days and a year is 365 days. New files receive the writer's current UTC creation time.
    /// </remarks>
    public FileRolloverInterval RolloverInterval { get; set; } = DefaultRollingInterval;

    /// <summary>
    ///     Gets or sets the retention threshold, in bytes, for archives of each resolved base name, including
    ///     group and level suffixes. On rollover or initial file creation, if qualifying archives meet or exceed
    ///     this value, the oldest archive by creation time is deleted. Defaults to
    ///     <see cref="DefaultMaxTotalSizeBytes" /> (10 GiB).
    /// </summary>
    /// <remarks>
    ///     At most one archive is deleted per check. Active destinations are excluded, and the active file's
    ///     size is not counted. This is not a hard disk quota. Zero or negative values delete the oldest
    ///     qualifying archive whenever a retention check finds one; they do not disable retention.
    /// </remarks>
    public long MaxTotalSizeBytes { get; set; } = DefaultMaxTotalSizeBytes;

    /// <summary>
    ///     Replaces <see cref="LogName" /> with <see cref="DefaultLogName" /> when the suffix is null,
    ///     or with <c>_{suffix}</c> otherwise.
    /// </summary>
    /// <param name="suffix">
    ///     The suffix used to construct a new name. Null resets to the default; an empty string produces <c>_</c>.
    /// </param>
    /// <remarks>
    ///     <see cref="DefaultLogName" /> is null, so a nonnull suffix does not include the application's name
    ///     or the current <see cref="LogName" />. For example, <c>SetLogNameSuffix("worker")</c> produces
    ///     <c>_worker.log</c> before group and level suffixes are applied.
    /// </remarks>
    public void SetLogNameSuffix(string? suffix) =>
        LogName = suffix is null
            ? DefaultLogName
            : $"{DefaultLogName}_{suffix}";
}
