namespace Invex.Extensions.Logging.FancyConsole.Configuration;

/// <summary>
///     Configuration options for the fancy console logger. Bound from the <c>Logging:FancyConsole</c>
///     configuration section when registered via
///     <see cref="FancyConsoleLoggerExtensions.AddFancyConsole(Microsoft.Extensions.Logging.ILoggingBuilder)" />,
///     and can also be set programmatically via the
///     <see
///         cref="FancyConsoleLoggerExtensions.AddFancyConsole(Microsoft.Extensions.Logging.ILoggingBuilder, System.Action{FancyConsoleLoggerConfiguration})" />
///     overload.
/// </summary>
/// <remarks>
///     Changes from configuration sources that support reload (for example, <c>appsettings.json</c> loaded with
///     <c>reloadOnChange</c>) are applied to subsequent log entries. Programmatic configuration delegates are
///     reapplied when options are rebuilt, so their values continue to override bound configuration.
///     Messages, categories, and scopes are written as literal text, not Spectre.Console markup.
/// </remarks>
[PublicAPI]
public sealed class FancyConsoleLoggerConfiguration
{
    /// <summary>
    ///     The default value of <see cref="Layout" />: <see cref="FancyConsoleLayout.Standard" />.
    /// </summary>
    public const FancyConsoleLayout DefaultLayout = FancyConsoleLayout.Standard;

    /// <summary>
    ///     The default value of <see cref="TimestampFormat" />: <see langword="null" />, which uses the layout's
    ///     default format.
    /// </summary>
    public const string? DefaultTimestampFormat = null;

    /// <summary>
    ///     The default value of <see cref="UseUtcTimestamp" />: <see langword="false" />.
    /// </summary>
    public const bool DefaultUseUtcTimestamp = false;

    /// <summary>
    ///     The default value of <see cref="IncludeScopes" />: <see langword="false" />.
    /// </summary>
    public const bool DefaultIncludeScopes = false;

    /// <summary>
    ///     The default value of <see cref="UseShortCategoryName" />: <see langword="false" />.
    /// </summary>
    public const bool DefaultUseShortCategoryName = false;

    /// <summary>
    ///     The default value of <see cref="UseColors" />: <see langword="true" />.
    /// </summary>
    public const bool DefaultUseColors = true;

    /// <summary>
    ///     The default value of <see cref="ExceptionFormat" />: <see cref="FancyConsoleExceptionFormat.Full" />.
    /// </summary>
    public const FancyConsoleExceptionFormat DefaultExceptionFormat = FancyConsoleExceptionFormat.Full;

    /// <summary>
    ///     The default value of <see cref="ExceptionTextStyle" />: <c>"red1"</c>.
    /// </summary>
    public const string DefaultExceptionTextStyle = "red1";

    /// <summary>
    ///     The default value of <see cref="LogToStandardErrorThreshold" />: <see cref="LogLevel.None" />, which
    ///     writes every entry to standard output.
    /// </summary>
    public const LogLevel DefaultLogToStandardErrorThreshold = LogLevel.None;

    /// <summary>
    ///     Gets or sets how each log entry is laid out. Defaults to <see cref="DefaultLayout" />
    ///     (<see cref="FancyConsoleLayout.Standard" />).
    /// </summary>
    public FancyConsoleLayout Layout { get; set; } = DefaultLayout;

    /// <summary>
    ///     Gets or sets a .NET date and time format string for entry timestamps, formatted with the invariant
    ///     culture. When <see langword="null" /> or empty (the default), the layout's default is used:
    ///     <c>HH:mm:ss.fff</c> for <see cref="FancyConsoleLayout.Standard" /> and
    ///     <see cref="FancyConsoleLayout.SingleLine" />, and <c>yyyy-MM-dd HH:mm:ss.fff zzz</c> for
    ///     <see cref="FancyConsoleLayout.Detailed" />. The <see cref="FancyConsoleLayout.Standard" /> header line
    ///     always shows the date and UTC offset. Ignored by <see cref="FancyConsoleLayout.Minimal" />.
    /// </summary>
    /// <remarks>
    ///     A format that raises <see cref="FormatException" /> falls back to the layout's default. Formatting
    ///     controls the displayed timestamp only; it does not change when entries are captured.
    /// </remarks>
    public string? TimestampFormat { get; set; } = DefaultTimestampFormat;

    /// <summary>
    ///     Gets or sets whether timestamps are shown in UTC rather than local time. Defaults to
    ///     <see cref="DefaultUseUtcTimestamp" /> (<see langword="false" />).
    /// </summary>
    public bool UseUtcTimestamp { get; set; } = DefaultUseUtcTimestamp;

    /// <summary>
    ///     Gets or sets whether active logging scopes are shown after the category, separated by <c> =&gt; </c>,
    ///     in the <see cref="FancyConsoleLayout.Standard" /> and <see cref="FancyConsoleLayout.SingleLine" />
    ///     layouts. <see cref="FancyConsoleLayout.Detailed" /> always shows scopes and
    ///     <see cref="FancyConsoleLayout.Minimal" /> never does. Defaults to <see cref="DefaultIncludeScopes" />
    ///     (<see langword="false" />).
    /// </summary>
    /// <remarks>
    ///     Message-template scopes (for example, <c>logger.BeginScope("Order {OrderId}", 42)</c>) are shown as
    ///     their formatted text. Other key/value scopes are shown as comma-separated <c>Key=Value</c> pairs, and
    ///     any other scope object is shown using <see cref="object.ToString" />. Scopes are shown outermost first.
    ///     Null and empty scope text is skipped; whitespace and line breaks are preserved.
    /// </remarks>
    public bool IncludeScopes { get; set; } = DefaultIncludeScopes;

    /// <summary>
    ///     Gets or sets whether categories are shortened to the text after their last <c>.</c> outside generic
    ///     arguments (for example,
    ///     <c>MyApp.Services.OrderService</c> becomes <c>OrderService</c>). Defaults to
    ///     <see cref="DefaultUseShortCategoryName" /> (<see langword="false" />).
    /// </summary>
    /// <remarks>
    ///     Dots inside generic arguments are ignored: <c>MyApp.Repository&lt;MyApp.Models.Order&gt;</c> becomes
    ///     <c>Repository&lt;MyApp.Models.Order&gt;</c>. Categories with no qualifying dot or a trailing dot are unchanged.
    ///     Shortening affects display only; framework category filters still use the original category.
    /// </remarks>
    public bool UseShortCategoryName { get; set; } = DefaultUseShortCategoryName;

    /// <summary>
    ///     Gets or sets whether level and exception styles are applied. When <see langword="false" />, all output
    ///     is written without styling, including <see cref="FancyConsoleExceptionFormat.Pretty" /> exceptions.
    ///     Defaults to <see cref="DefaultUseColors" /> (<see langword="true" />).
    /// </summary>
    /// <remarks>
    ///     Even when enabled, colors are only emitted if the console supports them. Spectre.Console detects
    ///     terminal capabilities and honors the <c>NO_COLOR</c> environment variable.
    /// </remarks>
    public bool UseColors { get; set; } = DefaultUseColors;

    /// <summary>
    ///     Gets or sets per-<see cref="LogLevel" /> Spectre.Console style strings (for example, <c>"bold red"</c>,
    ///     <c>"#ff8800"</c>, or <c>"black on yellow"</c>) used for each entry's level-colored text. A mapped
    ///     <see langword="null" /> or whitespace value disables styling for that level. Missing levels and values
    ///     that are not valid styles use the built-in defaults: <c>grey</c> (Trace), <c>seagreen3</c> (Debug),
    ///     <c>skyblue1</c> (Information), <c>gold1</c> (Warning), <c>darkorange</c> (Error), and <c>fuchsia</c>
    ///     (Critical). Empty by default.
    /// </summary>
    /// <remarks>
    ///     Styles apply to headers, level codes, and Detailed field labels. Message text remains unstyled, and
    ///     exception text uses <see cref="ExceptionTextStyle" /> or the Pretty formatter's own styles.
    /// </remarks>
    public Dictionary<LogLevel, string?> LevelStyles { get; set; } = [];

    /// <summary>
    ///     Gets or sets how exceptions attached to log entries are rendered. Defaults to
    ///     <see cref="DefaultExceptionFormat" /> (<see cref="FancyConsoleExceptionFormat.Full" />).
    /// </summary>
    public FancyConsoleExceptionFormat ExceptionFormat { get; set; } = DefaultExceptionFormat;

    /// <summary>
    ///     Gets or sets the Spectre.Console style string used for <see cref="FancyConsoleExceptionFormat.Full" />
    ///     and <see cref="FancyConsoleExceptionFormat.Summary" /> exception text. A <see langword="null" /> or
    ///     whitespace value disables styling; a value that is not a valid style uses
    ///     <see cref="DefaultExceptionTextStyle" />. Defaults to <see cref="DefaultExceptionTextStyle" />
    ///     (<c>"red1"</c>).
    /// </summary>
    public string? ExceptionTextStyle { get; set; } = DefaultExceptionTextStyle;

    /// <summary>
    ///     Gets or sets the minimum level written to standard error instead of standard output. Defaults to
    ///     <see cref="DefaultLogToStandardErrorThreshold" /> (<see cref="LogLevel.None" />), which writes every
    ///     entry to standard output.
    /// </summary>
    /// <remarks>
    ///     The entire entry, including scopes and exception output, is sent to the selected stream. Routing
    ///     uses the entry's level, not whether it carries an exception, and does not filter entries.
    /// </remarks>
    public LogLevel LogToStandardErrorThreshold { get; set; } = DefaultLogToStandardErrorThreshold;
}
