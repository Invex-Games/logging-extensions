namespace Invex.Extensions.Logging.FancyConsole;

/// <summary>
///     Provides extension methods for registering the fancy console logger.
/// </summary>
[PublicAPI]
public static class FancyConsoleLoggerExtensions
{
    /// <param name="builder">The <see cref="ILoggingBuilder" /> to add the fancy console logger to.</param>
    extension(ILoggingBuilder builder)
    {
        /// <summary>
        ///     Adds a fancy console logger provider to the logging pipeline using configuration bound from the
        ///     <c>Logging:FancyConsole</c> configuration section (see <see cref="FancyConsoleLoggerConfiguration" />).
        /// </summary>
        /// <returns>The same <see cref="ILoggingBuilder" /> instance so that additional calls can be chained.</returns>
        /// <remarks>
        ///     The provider is registered with the alias <c>"FancyConsole"</c>, so it can be configured and filtered via
        ///     the <c>Logging:FancyConsole</c> configuration section. Calling this method multiple times registers the
        ///     provider only once. Existing logging providers remain registered; call
        ///     <c>ClearProviders()</c> first when replacing the default console provider.
        /// </remarks>
        public ILoggingBuilder AddFancyConsole()
        {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(
                ServiceDescriptor.Singleton<ILoggerProvider, FancyConsoleLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions<FancyConsoleLoggerConfiguration, FancyConsoleLoggerProvider>(
                builder.Services);

            return builder;
        }

        /// <summary>
        ///     Adds a fancy console logger provider to the logging pipeline and applies additional configuration via a
        ///     delegate.
        /// </summary>
        /// <param name="configure">
        ///     A delegate that configures the <see cref="FancyConsoleLoggerConfiguration" />. Values set here override
        ///     values bound from the <c>Logging:FancyConsole</c> configuration section.
        /// </param>
        /// <returns>The same <see cref="ILoggingBuilder" /> instance so that additional calls can be chained.</returns>
        /// <remarks>
        ///     The delegate is applied whenever options are created, including after a configuration reload.
        ///     Repeated calls register one provider but add each supplied configuration delegate in call order.
        /// </remarks>
        public ILoggingBuilder AddFancyConsole(Action<FancyConsoleLoggerConfiguration> configure)
        {
            builder.AddFancyConsole();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
