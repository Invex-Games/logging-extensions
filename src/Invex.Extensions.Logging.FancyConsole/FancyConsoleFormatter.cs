namespace Invex.Extensions.Logging.FancyConsole;

/// <summary>
///     Converts a <see cref="FancyConsoleLogEntry" /> into Spectre.Console renderables according to the configured
///     <see cref="FancyConsoleLayout" />, <see cref="FancyConsoleExceptionFormat" />, and styles.
/// </summary>
/// <remarks>
///     All entry text is escaped, so messages, categories, scopes, and exception text are never interpreted as
///     Spectre.Console markup. Invalid configured styles and timestamp formats fall back to their defaults rather
///     than failing the entry.
/// </remarks>
internal static class FancyConsoleFormatter
{
    /// <summary>
    ///     The default time format used by the <see cref="FancyConsoleLayout.Standard" /> and
    ///     <see cref="FancyConsoleLayout.SingleLine" /> layouts.
    /// </summary>
    private const string DefaultTimeFormat = "HH:mm:ss.fff";

    /// <summary>
    ///     The default timestamp format used by the <see cref="FancyConsoleLayout.Detailed" /> layout.
    /// </summary>
    private const string DefaultDetailedTimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz";

    /// <summary>
    ///     The format of the date and UTC offset shown in the <see cref="FancyConsoleLayout.Standard" /> header.
    /// </summary>
    private const string StandardHeaderFormat = "yyyy-MM-dd zzz";

    /// <summary>
    ///     The indentation used for continuation and exception lines in the compact layouts.
    /// </summary>
    private const int CompactIndent = 4;

    /// <summary>
    ///     The scope property key that identifies a message-template scope.
    /// </summary>
    private const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>
    ///     The separator placed between the category and each scope, and between scopes.
    /// </summary>
    private const string ScopeSeparator = " => ";

    /// <summary>
    ///     The separator placed before an exception summary written on the same line as the message.
    /// </summary>
    private const string InlineExceptionSeparator = " | ";

    /// <summary>
    ///     The width of the padded field labels in the <see cref="FancyConsoleLayout.Detailed" /> layout.
    /// </summary>
    private const int DetailedLabelWidth = 11;

    /// <summary>
    ///     The leading indentation of field labels in the <see cref="FancyConsoleLayout.Detailed" /> layout.
    /// </summary>
    private const int DetailedLabelIndent = 2;

    /// <summary>
    ///     The column at which field values start in the <see cref="FancyConsoleLayout.Detailed" /> layout.
    /// </summary>
    private const int DetailedValueIndent = DetailedLabelIndent + DetailedLabelWidth;

    /// <summary>
    ///     The Spectre.Console exception formats used for <see cref="FancyConsoleExceptionFormat.Pretty" />.
    /// </summary>
    private const ExceptionFormats PrettyExceptionFormats =
        ExceptionFormats.ShortenPaths | ExceptionFormats.ShortenTypes | ExceptionFormats.ShortenMethods;

    /// <summary>
    ///     Formats a log entry as renderables to be written to the console in order.
    /// </summary>
    /// <param name="entry">The entry to format.</param>
    /// <param name="config">The configuration controlling layout and styling.</param>
    /// <returns>The renderables representing the entry, ending with a line break.</returns>
    public static IReadOnlyList<IRenderable> Format(FancyConsoleLogEntry entry, FancyConsoleLoggerConfiguration config)
    {
        var output = new EntryBuilder();

        switch (config.Layout)
        {
            case FancyConsoleLayout.SingleLine:
                FormatSingleLine(output, entry, config);

                break;

            case FancyConsoleLayout.Minimal:
                FormatMinimal(output, entry, config);

                break;

            case FancyConsoleLayout.Detailed:
                FormatDetailed(output, entry, config);

                break;

            case FancyConsoleLayout.Standard:
            default:
                FormatStandard(output, entry, config);

                break;
        }

        return output.Build();
    }

    /// <summary>
    ///     Formats a scope object for display.
    /// </summary>
    /// <param name="scope">The scope state supplied to <see cref="ILogger.BeginScope{TState}" />.</param>
    /// <returns>
    ///     The formatted message for message-template scopes, comma-separated <c>Key=Value</c> pairs for other
    ///     key/value scopes, or <see cref="object.ToString" /> otherwise. <see langword="null" /> when the scope is
    ///     <see langword="null" />.
    /// </returns>
    public static string? FormatScope(object? scope)
    {
        if (scope is null)
            return null;

        if (scope is not IEnumerable<KeyValuePair<string, object?>> properties)
            return scope.ToString();

        var pairs = properties.ToList();

        return pairs.Exists(static pair => pair.Key == OriginalFormatKey)
            ? scope.ToString()
            : string.Join(", ", pairs.Select(static pair => $"{pair.Key}={pair.Value}"));
    }

    /// <summary>
    ///     Builds a summary of an exception and its <see cref="Exception.InnerException" /> chain, preserving
    ///     any line breaks in exception messages for the layout to handle.
    /// </summary>
    /// <param name="exception">The outermost exception.</param>
    /// <returns><c>{TypeName}: {Message}</c> for each exception, joined by <c> ---&gt; </c>.</returns>
    private static string GetExceptionSummary(Exception exception)
    {
        var summary = new StringBuilder();

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (summary.Length > 0)
                summary.Append(" ---> ");

            summary
                .Append(current.GetType()
                    .Name)
                .Append(": ")
                .Append(current.Message);
        }

        return summary.ToString();
    }

    /// <summary>
    ///     Formats an entry using <see cref="FancyConsoleLayout.Standard" />.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="entry">The captured entry.</param>
    /// <param name="config">The layout, timestamp, scope, and style options.</param>
    private static void FormatStandard(
        EntryBuilder output,
        FancyConsoleLogEntry entry,
        FancyConsoleLoggerConfiguration config)
    {
        var levelStyle = GetLevelStyle(entry.LogLevel, config);

        output
            .Append(
                $"{FormatTimestamp(entry.Timestamp, StandardHeaderFormat, StandardHeaderFormat)} {GetCategory(entry, config)}{GetScopeSuffix(entry, config.IncludeScopes)}",
                levelStyle)
            .AppendLine();

        var prefix =
            $"{FormatTimestamp(entry.Timestamp, config.TimestampFormat, DefaultTimeFormat)}  {GetLevelCode(entry.LogLevel)}";

        var indent = prefix.Length + 1;

        output
            .Append(prefix, levelStyle)
            .Append(" ")
            .AppendLines(entry.Message, 0, indent)
            .AppendLine();

        AppendExceptionBlock(output, entry.Exception, config, indent, false);

        output.AppendLine();
    }

    /// <summary>
    ///     Formats an entry using <see cref="FancyConsoleLayout.SingleLine" />.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="entry">The captured entry.</param>
    /// <param name="config">The layout, timestamp, scope, and style options.</param>
    private static void FormatSingleLine(
        EntryBuilder output,
        FancyConsoleLogEntry entry,
        FancyConsoleLoggerConfiguration config)
    {
        var prefix =
            $"{FormatTimestamp(entry.Timestamp, config.TimestampFormat, DefaultTimeFormat)} {GetLevelCode(entry.LogLevel)} {GetCategory(entry, config)}{GetScopeSuffix(entry, config.IncludeScopes)}:";

        output
            .Append(prefix, GetLevelStyle(entry.LogLevel, config))
            .Append(" ")
            .Append(Flatten(entry.Message));

        AppendInlineExceptionSummary(output, entry.Exception, config);
        output.AppendLine();
        AppendExceptionBlock(output, entry.Exception, config, CompactIndent, true);
    }

    /// <summary>
    ///     Formats an entry using <see cref="FancyConsoleLayout.Minimal" />.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="entry">The captured entry.</param>
    /// <param name="config">The exception and style options.</param>
    private static void FormatMinimal(
        EntryBuilder output,
        FancyConsoleLogEntry entry,
        FancyConsoleLoggerConfiguration config)
    {
        output
            .Append(GetLevelCode(entry.LogLevel), GetLevelStyle(entry.LogLevel, config))
            .Append(" ")
            .AppendLines(entry.Message, 0, CompactIndent);

        AppendInlineExceptionSummary(output, entry.Exception, config);
        output.AppendLine();
        AppendExceptionBlock(output, entry.Exception, config, CompactIndent, true);
    }

    /// <summary>
    ///     Formats an entry using <see cref="FancyConsoleLayout.Detailed" />.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="entry">The captured entry, including all nonempty active scopes.</param>
    /// <param name="config">The timestamp, category, exception, and style options.</param>
    private static void FormatDetailed(
        EntryBuilder output,
        FancyConsoleLogEntry entry,
        FancyConsoleLoggerConfiguration config)
    {
        var levelStyle = GetLevelStyle(entry.LogLevel, config);

        output
            .Append(
                $"{FormatTimestamp(entry.Timestamp, config.TimestampFormat, DefaultDetailedTimestampFormat)} {entry.LogLevel}",
                levelStyle)
            .AppendLine();

        AppendField(output, "Category", GetCategory(entry, config), levelStyle);

        if (entry.EventId.Id != 0 || !string.IsNullOrEmpty(entry.EventId.Name))
            AppendField(output,
                "Event",
                string.IsNullOrEmpty(entry.EventId.Name)
                    ? entry.EventId.Id.ToString(CultureInfo.InvariantCulture)
                    : $"{entry.EventId.Id.ToString(CultureInfo.InvariantCulture)} ({entry.EventId.Name})",
                levelStyle);

        AppendField(output, "Thread", entry.ThreadId.ToString(CultureInfo.InvariantCulture), levelStyle);

        if (entry.Scopes.Count > 0)
            AppendField(output, "Scopes", string.Join(ScopeSeparator, entry.Scopes), levelStyle);

        AppendField(output, "Message", entry.Message, levelStyle);

        if (entry.Exception is not null)
            switch (config.ExceptionFormat)
            {
                case FancyConsoleExceptionFormat.Pretty
                    when TryGetPrettyException(entry.Exception, config, DetailedValueIndent, out var pretty):
                    output
                        .Append(new(' ', DetailedLabelIndent))
                        .Append("Exception:", levelStyle)
                        .AppendLine()
                        .AppendRenderable(pretty);

                    break;

                case FancyConsoleExceptionFormat.Full:
                case FancyConsoleExceptionFormat.Pretty:
                    AppendField(output,
                        "Exception",
                        entry.Exception.ToString(),
                        levelStyle,
                        GetExceptionTextStyle(config));

                    break;

                case FancyConsoleExceptionFormat.Summary:
                    AppendField(output,
                        "Exception",
                        GetExceptionSummary(entry.Exception),
                        levelStyle,
                        GetExceptionTextStyle(config));

                    break;
            }

        output.AppendLine();
    }

    /// <summary>
    ///     Appends a labeled <see cref="FancyConsoleLayout.Detailed" /> field, aligning continuation lines with the
    ///     value column.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="label">The field label without its trailing colon.</param>
    /// <param name="value">The field's literal text.</param>
    /// <param name="labelStyle">The style for the field label, or <see langword="null" /> for no styling.</param>
    /// <param name="valueStyle">The style for the field value, or <see langword="null" /> for no styling.</param>
    private static void AppendField(
        EntryBuilder output,
        string label,
        string value,
        string? labelStyle,
        string? valueStyle = null)
    {
        var labelText = $"{label}:";

        output
            .Append(new(' ', DetailedLabelIndent))
            .Append(labelText, labelStyle)
            .Append(new(' ', Math.Max(1, DetailedLabelWidth - labelText.Length)))
            .AppendLines(value, 0, DetailedValueIndent, valueStyle)
            .AppendLine();
    }

    /// <summary>
    ///     Appends an exception summary to the current line when the compact layouts use
    ///     <see cref="FancyConsoleExceptionFormat.Summary" />.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="exception">The exception to summarize, if any.</param>
    /// <param name="config">The exception format and style options.</param>
    private static void AppendInlineExceptionSummary(
        EntryBuilder output,
        Exception? exception,
        FancyConsoleLoggerConfiguration config)
    {
        if (exception is null || config.ExceptionFormat is not FancyConsoleExceptionFormat.Summary)
            return;

        output
            .Append(InlineExceptionSeparator)
            .Append(Flatten(GetExceptionSummary(exception)), GetExceptionTextStyle(config));
    }

    /// <summary>
    ///     Appends an exception on the lines following the message, indented by <paramref name="indent" />.
    /// </summary>
    /// <param name="output">The entry being built.</param>
    /// <param name="exception">The exception to append, if any.</param>
    /// <param name="config">The configuration controlling the exception format and style.</param>
    /// <param name="indent">The number of spaces before each exception line.</param>
    /// <param name="summaryIsInline">Whether summaries were already written on the message line.</param>
    private static void AppendExceptionBlock(
        EntryBuilder output,
        Exception? exception,
        FancyConsoleLoggerConfiguration config,
        int indent,
        bool summaryIsInline)
    {
        if (exception is null)
            return;

        switch (config.ExceptionFormat)
        {
            case FancyConsoleExceptionFormat.Pretty
                when TryGetPrettyException(exception, config, indent, out var pretty):
                output.AppendRenderable(pretty);

                break;

            case FancyConsoleExceptionFormat.Full:
            case FancyConsoleExceptionFormat.Pretty:
                output
                    .AppendLines(exception.ToString(), indent, indent, GetExceptionTextStyle(config))
                    .AppendLine();

                break;

            case FancyConsoleExceptionFormat.Summary when !summaryIsInline:
                output
                    .AppendLines(GetExceptionSummary(exception), indent, indent, GetExceptionTextStyle(config))
                    .AppendLine();

                break;
        }
    }

    /// <summary>
    ///     Creates a Spectre.Console exception renderable, indented by <paramref name="indent" />.
    /// </summary>
    /// <remarks>
    ///     Spectre.Console cannot render some exceptions on every target framework (for example, exceptions with inner
    ///     exceptions that were never thrown on .NET Framework); callers fall back to the full format in that case.
    /// </remarks>
    /// <param name="exception">The exception to render.</param>
    /// <param name="config">The configuration controlling whether colors are used.</param>
    /// <param name="indent">The number of spaces before each exception line.</param>
    /// <param name="renderable">The indented exception renderable, if it could be created.</param>
    /// <returns><see langword="true" /> if the renderable was created; otherwise, <see langword="false" />.</returns>
    private static bool TryGetPrettyException(
        Exception exception,
        FancyConsoleLoggerConfiguration config,
        int indent,
        out IRenderable renderable)
    {
        IRenderable exceptionRenderable;

        try
        {
            exceptionRenderable = exception.GetRenderable(new ExceptionSettings
            {
                Format = PrettyExceptionFormats,
            });
        }
        catch (Exception ex) when (ex is NullReferenceException or InvalidOperationException or ArgumentException)
        {
            renderable = null!;

            return false;
        }

        if (!config.UseColors)
            exceptionRenderable = new UnstyledRenderable(exceptionRenderable);

        renderable = new Padder(exceptionRenderable, new Padding(indent, 0, 0, 0));

        return true;
    }

    /// <summary>
    ///     Formats a timestamp using the invariant culture, falling back to <paramref name="defaultFormat" /> when
    ///     <paramref name="format" /> is missing or invalid.
    /// </summary>
    /// <param name="timestamp">The captured local or UTC timestamp.</param>
    /// <param name="format">The requested format, or <see langword="null" /> to use the default.</param>
    /// <param name="defaultFormat">The valid layout default used when the requested format is missing or invalid.</param>
    /// <returns>The formatted timestamp.</returns>
    private static string FormatTimestamp(DateTimeOffset timestamp, string? format, string defaultFormat)
    {
        if (string.IsNullOrEmpty(format))
            return timestamp.ToString(defaultFormat, CultureInfo.InvariantCulture);

        try
        {
            return timestamp.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            return timestamp.ToString(defaultFormat, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    ///     Gets the category to display, shortened when <see cref="FancyConsoleLoggerConfiguration.UseShortCategoryName" />
    ///     is enabled. Dots inside generic arguments are ignored when shortening.
    /// </summary>
    /// <param name="entry">The entry containing the full logger category.</param>
    /// <param name="config">The options controlling category shortening.</param>
    /// <returns>The display category, which may differ from the category used for framework filtering.</returns>
    private static string GetCategory(FancyConsoleLogEntry entry, FancyConsoleLoggerConfiguration config)
    {
        var category = entry.Category;

        if (!config.UseShortCategoryName || category.Length == 0)
            return category;

        var genericDepth = 0;
        var lastDot = -1;

        for (var index = category.Length - 1; index >= 0; index--)
            if (category[index] == '>')
            {
                genericDepth++;
            }
            else if (category[index] == '<' && genericDepth > 0)
            {
                genericDepth--;
            }
            else if (category[index] == '.' && genericDepth == 0)
            {
                lastDot = index;

                break;
            }

        return lastDot >= 0 && lastDot < category.Length - 1
            ? category.Substring(lastDot + 1)
            : category;
    }

    /// <summary>
    ///     Gets the scopes to display after the category, or an empty string when scopes are excluded or absent.
    /// </summary>
    /// <param name="entry">The entry containing formatted scopes.</param>
    /// <param name="includeScopes">Whether to show captured scopes.</param>
    /// <returns>The scope separator followed by the joined scopes, or an empty string.</returns>
    private static string GetScopeSuffix(FancyConsoleLogEntry entry, bool includeScopes) =>
        includeScopes && entry.Scopes.Count > 0
            ? ScopeSeparator + string.Join(ScopeSeparator, entry.Scopes)
            : string.Empty;

    /// <summary>
    ///     Gets the three-letter code for a log level.
    /// </summary>
    /// <param name="logLevel">The level to display.</param>
    /// <returns>The level code, or <c>???</c> for an unknown level.</returns>
    private static string GetLevelCode(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???",
        };

    /// <summary>
    ///     Gets the built-in style for a log level.
    /// </summary>
    /// <param name="logLevel">The level to style.</param>
    /// <returns>The default style, or <see langword="null" /> for an unknown level.</returns>
    private static string? GetDefaultLevelStyle(LogLevel logLevel) =>
        logLevel switch
        {
            LogLevel.Trace => "grey",
            LogLevel.Debug => "seagreen3",
            LogLevel.Information => "skyblue1",
            LogLevel.Warning => "gold1",
            LogLevel.Error => "darkorange",
            LogLevel.Critical => "fuchsia",
            _ => null,
        };

    /// <summary>
    ///     Gets the effective style for a log level, or <see langword="null" /> for no styling.
    /// </summary>
    /// <param name="logLevel">The level to style.</param>
    /// <param name="config">The options controlling colors and per-level overrides.</param>
    /// <returns>The valid configured or default style, or <see langword="null" /> when styling is disabled.</returns>
    private static string? GetLevelStyle(LogLevel logLevel, FancyConsoleLoggerConfiguration config)
    {
        if (!config.UseColors)
            return null;

        var defaultStyle = GetDefaultLevelStyle(logLevel);

        return config.LevelStyles.TryGetValue(logLevel, out var style)
            ? ResolveStyle(style, defaultStyle)
            : defaultStyle;
    }

    /// <summary>
    ///     Gets the effective exception text style, or <see langword="null" /> for no styling.
    /// </summary>
    /// <param name="config">The options controlling colors and exception text styling.</param>
    /// <returns>The valid configured or default style, or <see langword="null" /> when styling is disabled.</returns>
    private static string? GetExceptionTextStyle(FancyConsoleLoggerConfiguration config) =>
        config.UseColors
            ? ResolveStyle(config.ExceptionTextStyle, FancyConsoleLoggerConfiguration.DefaultExceptionTextStyle)
            : null;

    /// <summary>
    ///     Validates a configured style, returning <see langword="null" /> for blank styles and
    ///     <paramref name="fallback" /> for invalid styles.
    /// </summary>
    /// <param name="style">The configured Spectre.Console style string.</param>
    /// <param name="fallback">The default to use if the configured style cannot be parsed.</param>
    /// <returns>The trimmed valid style, the fallback, or <see langword="null" /> for blank input.</returns>
    private static string? ResolveStyle(string? style, string? fallback)
    {
        if (style is null || string.IsNullOrWhiteSpace(style))
            return null;

        var trimmed = style.Trim();

        return Style.TryParse(trimmed, out _)
            ? trimmed
            : fallback;
    }

    /// <summary>
    ///     Replaces line breaks with spaces.
    /// </summary>
    /// <param name="text">The literal text to flatten.</param>
    /// <returns>The text with CRLF pairs and remaining CR or LF characters replaced by spaces.</returns>
    private static string Flatten(string text) =>
        text
            .Replace("\r\n", " ")
            .Replace('\n', ' ')
            .Replace('\r', ' ');

    /// <summary>
    ///     Accumulates escaped markup and other renderables for a single entry.
    /// </summary>
    private sealed class EntryBuilder
    {
        /// <summary>
        ///     The escaped markup accumulated since the last renderable was added.
        /// </summary>
        private readonly StringBuilder _markup = new();

        /// <summary>
        ///     Completed markup and exception renderables, in write order.
        /// </summary>
        private readonly List<IRenderable> _renderables = [];

        /// <summary>
        ///     Appends escaped text, wrapped in a style tag when <paramref name="style" /> is set.
        /// </summary>
        /// <param name="text">The literal text to append.</param>
        /// <param name="style">The valid style to apply, or <see langword="null" /> for no styling.</param>
        /// <returns>This builder.</returns>
        public EntryBuilder Append(string text, string? style = null)
        {
            if (text.Length == 0)
                return this;

            if (style is null)
                _markup.Append(Markup.Escape(text));
            else
                _markup
                    .Append('[')
                    .Append(style)
                    .Append(']')
                    .Append(Markup.Escape(text))
                    .Append("[/]");

            return this;
        }

        /// <summary>
        ///     Appends a line break.
        /// </summary>
        /// <returns>This builder.</returns>
        public EntryBuilder AppendLine()
        {
            _markup.Append('\n');

            return this;
        }

        /// <summary>
        ///     Appends multi-line text, indenting the first line by <paramref name="firstIndent" /> spaces and each
        ///     following line by <paramref name="restIndent" /> spaces. Indentation is never styled.
        /// </summary>
        /// <param name="text">The literal text, with CRLF and LF line breaks.</param>
        /// <param name="firstIndent">The indentation before the first line.</param>
        /// <param name="restIndent">The indentation before subsequent lines.</param>
        /// <param name="style">The valid style for the text, or <see langword="null" /> for no styling.</param>
        /// <returns>This builder.</returns>
        public EntryBuilder AppendLines(string text, int firstIndent, int restIndent, string? style = null)
        {
            var lines = text
                .Replace("\r\n", "\n")
                .Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    AppendLine();

                Append(new(' ',
                    i == 0
                        ? firstIndent
                        : restIndent));

                Append(lines[i], style);
            }

            return this;
        }

        /// <summary>
        ///     Appends a renderable after any pending markup.
        /// </summary>
        /// <param name="renderable">The renderable to add to the entry.</param>
        public void AppendRenderable(IRenderable renderable)
        {
            Flush();
            _renderables.Add(renderable);
        }

        /// <summary>
        ///     Completes the entry.
        /// </summary>
        /// <returns>The renderables in write order.</returns>
        public IReadOnlyList<IRenderable> Build()
        {
            Flush();

            return _renderables;
        }

        /// <summary>
        ///     Converts pending markup into a <see cref="Markup" /> renderable.
        /// </summary>
        private void Flush()
        {
            if (_markup.Length == 0)
                return;

            _renderables.Add(new Markup(_markup.ToString()));
            _markup.Clear();
        }
    }

    /// <summary>
    ///     Wraps a renderable and removes all styling from its segments, used when colors are disabled.
    /// </summary>
    /// <param name="inner">The renderable to strip styling from.</param>
    private sealed class UnstyledRenderable(IRenderable inner) : IRenderable
    {
        /// <inheritdoc />
        public Measurement Measure(RenderOptions options, int maxWidth) =>
            inner.Measure(options, maxWidth);

        /// <inheritdoc />
        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth) =>
            inner
                .Render(options, maxWidth)
                .Select(static segment => segment.IsLineBreak || segment.IsControlCode
                    ? segment
                    : new(segment.Text, Style.Plain));
    }
}
