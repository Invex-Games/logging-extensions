namespace Invex.Extensions.Logging.FancyConsole.Configuration;

/// <summary>
///     Specifies how the fancy console logger lays out each log entry.
/// </summary>
[PublicAPI]
public enum FancyConsoleLayout
{
    /// <summary>
    ///     A two-line block followed by a blank line. The first line shows the date, UTC offset, and category;
    ///     the second shows the time, three-letter level code, and message. Continuation lines of the message
    ///     and exception are indented to align with the message.
    /// </summary>
    Standard = 0,

    /// <summary>
    ///     One line per entry: the time, three-letter level code, and category, followed by a colon and the
    ///     message. Line breaks in the message are replaced by spaces. Exceptions shown with
    ///     <see cref="FancyConsoleExceptionFormat.Summary" /> stay on the same line; other exception formats are
    ///     written on the following lines, indented by four spaces.
    /// </summary>
    SingleLine = 1,

    /// <summary>
    ///     Only the three-letter level code and message. Timestamps, categories, and scopes are omitted.
    ///     Continuation lines are indented by four spaces to align with the message.
    /// </summary>
    Minimal = 2,

    /// <summary>
    ///     A multi-line block followed by a blank line, showing the full timestamp and level name followed by
    ///     labeled fields for the category, event ID (when set), managed thread ID, scopes (when present),
    ///     message, and exception. Scopes are always included in this layout.
    /// </summary>
    Detailed = 3,
}
