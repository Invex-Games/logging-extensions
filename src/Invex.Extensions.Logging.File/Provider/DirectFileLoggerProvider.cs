namespace Invex.Extensions.Logging.File.Provider;

/// <summary>
///     An <see cref="ILoggerProvider" /> whose loggers write each entry to disk synchronously on the calling
///     thread (see <see cref="DirectFileLogWriter" />), guaranteeing the entry is persisted before the log
///     call returns. Registered by
///     <see cref="FileLoggerExtension.AddFile(Microsoft.Extensions.Logging.ILoggingBuilder, bool)" />
///     when <c>buffered</c> is <see langword="false" />.
/// </summary>
/// <param name="config">The options monitor providing the current <see cref="FileLoggerConfiguration" />.</param>
[PublicAPI]
[ProviderAlias("File")]
internal sealed class DirectFileLoggerProvider(IOptionsMonitor<FileLoggerConfiguration> config)
    : FileLoggerProvider(config), ILoggerProvider
{
    private DirectFileLogWriter? _logWriter;

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
