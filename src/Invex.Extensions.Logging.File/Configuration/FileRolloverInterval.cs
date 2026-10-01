namespace Invex.Extensions.Logging.File.Configuration;

/// <summary>
///     Specifies how frequently the active log file is rolled over to a new file, based on the elapsed time
///     since the file was created.
/// </summary>
/// <remarks>
///     Intervals are approximate elapsed durations rather than calendar boundaries:
///     <see cref="Year" /> corresponds to 365 days and <see cref="Month" /> to 30 days.
/// </remarks>
[PublicAPI]
public enum FileRolloverInterval
{
    /// <summary>Never roll over based on time. Files are still rolled over when they reach the size limit.</summary>
    Infinite = 0,

    /// <summary>Roll over after 365 days.</summary>
    Year = 1,

    /// <summary>Roll over after 30 days.</summary>
    Month = 2,

    /// <summary>Roll over after one day.</summary>
    Day = 3,

    /// <summary>Roll over after one hour.</summary>
    Hour = 4,

    /// <summary>Roll over after one minute.</summary>
    Minute = 5,
}
