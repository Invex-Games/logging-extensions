namespace Invex.Extensions.Logging.File.Provider;

/// <summary>
///     An <see cref="ILoggerProvider" /> whose loggers enqueue entries onto an in-memory channel that is
///     drained to disk by a dedicated background thread (see <see cref="BufferedFileLogWriter" />).
///     Registered by <see cref="FileLoggerExtension.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, bool)" />
///     when <c>buffered</c> is <see langword="true" />.
/// </summary>
/// <param name="config">The options monitor providing the current <see cref="FileLoggerConfiguration" />.</param>
[PublicAPI]
[ProviderAlias("File")]
internal sealed class BufferedFileLoggerProvider(IOptionsMonitor<FileLoggerConfiguration> config)
    : FileLoggerProvider(config), ILoggerProvider
{
    private BufferedFileLogWriter? _logWriter;

    /// <inheritdoc />
    protected override IFileLogWriter LogWriter => _logWriter ??= new(FileSystem, TimeProvider, GetCurrentConfig);

    /// <summary>
    ///     The file system abstraction used for all file operations. Replaceable for testing;
    ///     defaults to the real file system.
    /// </summary>
    internal static IFileSystem FileSystem { get; set; } = new FileSystem();

    /// <summary>
    ///     The time provider used for timestamps and rollover decisions. Replaceable for testing;
    ///     defaults to <see cref="TimeProvider.System" />.
    /// </summary>
    internal static TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
