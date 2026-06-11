namespace Invex.Extensions.Logging.File;

/// <summary>
///     Provides extension methods for registering the file logger with an <see cref="ILoggingBuilder" />.
/// </summary>
[PublicAPI]
public static class FileLoggerExtension
{
    /// <summary>
    ///     Adds a file logger provider to the logging pipeline using configuration bound from the
    ///     <c>Logging:File</c> configuration section (see <see cref="FileLoggerConfiguration" />).
    /// </summary>
    /// <param name="builder">The <see cref="ILoggingBuilder" /> to add the file logger to.</param>
    /// <param name="buffered">
    ///     When <see langword="true" /> (the default), log entries are queued and written to disk by a dedicated
    ///     background thread, minimizing logging overhead on application threads. When <see langword="false" />,
    ///     each log entry is written to disk synchronously on the calling thread, guaranteeing the entry is
    ///     persisted before the call returns.
    /// </param>
    /// <returns>The same <see cref="ILoggingBuilder" /> instance so that additional calls can be chained.</returns>
    /// <remarks>
    ///     The provider is registered with the alias <c>"File"</c>, so it can be configured via the
    ///     <c>Logging:File</c> configuration section. Calling this method multiple times registers the
    ///     provider only once.
    /// </remarks>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, bool buffered = true)
    {
        builder.AddConfiguration();

        if (buffered)
        {
            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, BufferedFileLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions<FileLoggerConfiguration, BufferedFileLoggerProvider>(
                builder.Services);
        }
        else
        {
            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, DirectFileLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions<FileLoggerConfiguration, DirectFileLoggerProvider>(
                builder.Services);
        }

        return builder;
    }

    /// <summary>
    ///     Adds a file logger provider to the logging pipeline and applies additional configuration via a delegate.
    /// </summary>
    /// <param name="builder">The <see cref="ILoggingBuilder" /> to add the file logger to.</param>
    /// <param name="configure">
    ///     A delegate that configures the <see cref="FileLoggerConfiguration" />. Values set here override
    ///     values bound from the <c>Logging:File</c> configuration section.
    /// </param>
    /// <param name="buffered">
    ///     When <see langword="true" /> (the default), log entries are queued and written to disk by a dedicated
    ///     background thread. When <see langword="false" />, each log entry is written to disk synchronously on
    ///     the calling thread.
    /// </param>
    /// <returns>The same <see cref="ILoggingBuilder" /> instance so that additional calls can be chained.</returns>
    public static ILoggingBuilder AddFile(
        this ILoggingBuilder builder,
        Action<FileLoggerConfiguration> configure,
        bool buffered = true)
    {
        builder.AddFile(buffered);
        builder.Services.Configure(configure);

        return builder;
    }
}
