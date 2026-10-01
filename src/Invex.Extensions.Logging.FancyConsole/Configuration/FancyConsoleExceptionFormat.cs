namespace Invex.Extensions.Logging.FancyConsole.Configuration;

/// <summary>
///     Specifies how the fancy console logger renders an exception attached to a log entry.
/// </summary>
[PublicAPI]
public enum FancyConsoleExceptionFormat
{
    /// <summary>
    ///     Writes the result of <see cref="Exception.ToString" />, including inner exceptions and stack traces,
    ///     using <see cref="FancyConsoleLoggerConfiguration.ExceptionTextStyle" />.
    /// </summary>
    Full = 0,

    /// <summary>
    ///     Writes the exception using Spectre.Console's exception formatter, which highlights the exception type,
    ///     message, methods, and source locations and shortens paths, types, and method names.
    /// </summary>
    Pretty = 1,

    /// <summary>
    ///     Writes only <c>{TypeName}: {Message}</c> for the exception and each inner exception, joined by
    ///     <c> ---&gt; </c>, without stack traces.
    /// </summary>
    Summary = 2,

    /// <summary>
    ///     Does not write exceptions. Only the formatted message is written.
    /// </summary>
    Hidden = 3,
}
