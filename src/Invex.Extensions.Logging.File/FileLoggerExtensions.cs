namespace Invex.Extensions.Logging.File;

/// <summary>
///     Provides extension methods for registering the file logger and creating group routing scopes.
/// </summary>
[PublicAPI]
public static class FileLoggerExtension
{
    /// <summary>
    ///     Begins a structured scope whose <c>Group</c> property selects a file name suffix from
    ///     <see cref="FileLoggerConfiguration.PerGroupLogName" />. Disposing the scope restores the outer
    ///     group, if any. Empty group names are ignored, leaving an outer group effective.
    /// </summary>
    /// <param name="logger">The logger on which to begin the scope.</param>
    /// <param name="groupName">The group name to match against the configured group mappings.</param>
    /// <returns>
    ///     The underlying scope token, or <see langword="null" /> when the logger does not create scopes.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="logger" /> is <see langword="null" />.</exception>
    /// <remarks>
    ///     This creates a standard logging scope, so other providers that consume scopes can also see its
    ///     <c>Group</c> property. The file logger uses the property for routing without rendering it in log lines.
    /// </remarks>
    public static IDisposable? BeginGroupScope(this ILogger logger, string? groupName) =>
        logger is null
            ? throw new ArgumentNullException(nameof(logger))
            : logger.BeginScope(new KeyValuePair<string, object?>[] { new(FileLogger.GroupScopeKey, groupName) });

    /// <param name="builder">The <see cref="ILoggingBuilder" /> to add the file logger to.</param>
    extension(ILoggingBuilder builder)
    {
        /// <summary>
        ///     Adds a file logger provider to the logging pipeline using configuration bound from the
        ///     <c>Logging:File</c> configuration section (see <see cref="FileLoggerConfiguration" />).
        /// </summary>
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
        public ILoggingBuilder AddFile(bool buffered = true)
        {
            builder.AddConfiguration();

            if (buffered)
            {
                builder.Services.TryAddEnumerable(ServiceDescriptor
                    .Singleton<ILoggerProvider, BufferedFileLoggerProvider>());

                LoggerProviderOptions.RegisterProviderOptions<FileLoggerConfiguration, BufferedFileLoggerProvider>(
                    builder.Services);
            }
            else
            {
                builder.Services.TryAddEnumerable(
                    ServiceDescriptor.Singleton<ILoggerProvider, DirectFileLoggerProvider>());

                LoggerProviderOptions.RegisterProviderOptions<FileLoggerConfiguration, DirectFileLoggerProvider>(
                    builder.Services);
            }

            return builder;
        }

        /// <summary>
        ///     Adds a file logger provider to the logging pipeline and applies additional configuration via a delegate.
        /// </summary>
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
        public ILoggingBuilder AddFile(Action<FileLoggerConfiguration> configure, bool buffered = true)
        {
            builder.AddFile(buffered);
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
