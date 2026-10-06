namespace Invex.Extensions.Logging.Utils.Tests;

/// <summary>
///     Exercises standalone factories with real framework filtering and dependency injection ownership.
/// </summary>
[TestFixture]
public sealed class LoggerFactoryTests
{
    /// <summary>
    ///     A factory created without configuration has no enabled logging destinations.
    /// </summary>
    [Test]
    public void CreateHostLoggerFactory_WithoutCallback_HasNoProviders()
    {
        using var factory = LogUtil.CreateHostLoggerFactory();
        var logger = factory.CreateLogger("Bootstrap");

        logger
            .IsEnabled(LogLevel.Critical)
            .ShouldBeFalse();

        Should.NotThrow(() => logger.LogCritical("No destination"));
    }

    /// <summary>
    ///     A lifecycle logger can be used without configuring any provider.
    /// </summary>
    [Test]
    public void CreateHostLogger_WithoutCallback_HasNoProviders()
    {
        using var logger = LogUtil.CreateHostLogger();

        logger
            .IsEnabled(LogLevel.Critical)
            .ShouldBeFalse();

        Should.NotThrow(() => logger.LogCritical("No destination"));
    }

    /// <summary>
    ///     Configuration is invoked exactly once before any providers have been registered by the helper.
    /// </summary>
    [Test]
    public void CreateHostLoggerFactory_InvokesCallbackOnceWithoutDefaultProviders()
    {
        var callbackCount = 0;

        using var factory = LogUtil.CreateHostLoggerFactory(builder =>
        {
            callbackCount++;

            builder
                .Services
                .Any(descriptor => descriptor.ServiceType == typeof(ILoggerProvider))
                .ShouldBeFalse();
        });

        callbackCount.ShouldBe(1);
    }

    /// <summary>
    ///     Microsoft categories are filtered at Warning while other categories retain their default threshold.
    /// </summary>
    /// <param name="category">The category being logged.</param>
    /// <param name="includeInformation">Whether the Information entry should reach the provider.</param>
    [TestCase("Microsoft", false)]
    [TestCase("Microsoft.Hosting.Lifetime", false)]
    [TestCase("MyApp.Services", true)]
    public void DefaultFilter_AppliesToMicrosoftCategories(string category, bool includeInformation)
    {
        var provider = new RecordingLoggerProvider();

        using var factory = LogUtil.CreateHostLoggerFactory(builder =>
            builder.Services.AddSingleton<ILoggerProvider>(_ => provider));

        var logger = factory.CreateLogger(category);

        logger
            .IsEnabled(LogLevel.Information)
            .ShouldBe(includeInformation);

        logger
            .IsEnabled(LogLevel.Warning)
            .ShouldBeTrue();

        logger.LogDebug("Debug");
        logger.LogInformation("Information");
        logger.LogWarning("Warning");
        logger.LogError("Error");

        var expected = includeInformation
            ? new[] { "Information", "Warning", "Error" }
            : new[] { "Warning", "Error" };

        provider
            .Loggers[category]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(expected);
    }

    /// <summary>
    ///     A callback's minimum level applies to ordinary categories without removing the Microsoft rule.
    /// </summary>
    [Test]
    public void Callback_CanSetMinimumLevel()
    {
        var provider = new RecordingLoggerProvider();

        using var factory = LogUtil.CreateHostLoggerFactory(builder =>
        {
            builder.Services.AddSingleton<ILoggerProvider>(_ => provider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        var applicationLogger = factory.CreateLogger("MyApp");
        var microsoftLogger = factory.CreateLogger("Microsoft.Hosting");

        applicationLogger.LogDebug("Application debug");
        microsoftLogger.LogDebug("Microsoft debug");
        microsoftLogger.LogWarning("Microsoft warning");

        provider
            .Loggers["MyApp"]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(["Application debug"]);

        provider
            .Loggers["Microsoft.Hosting"]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(["Microsoft warning"]);
    }

    /// <summary>
    ///     Later category rules in the callback override the helper's default Microsoft rule.
    /// </summary>
    /// <param name="minimum">The replacement minimum level.</param>
    [TestCase(LogLevel.Debug)]
    [TestCase(LogLevel.Error)]
    public void Callback_CanOverrideDefaultMicrosoftFilter(LogLevel minimum)
    {
        var provider = new RecordingLoggerProvider();

        using var factory = LogUtil.CreateHostLoggerFactory(builder =>
        {
            builder.Services.AddSingleton<ILoggerProvider>(_ => provider);
            builder.AddFilter("Microsoft", minimum);
        });

        var logger = factory.CreateLogger("Microsoft.Hosting");

        logger.LogDebug("Debug");
        logger.LogInformation("Information");
        logger.LogWarning("Warning");
        logger.LogError("Error");

        var expected = minimum == LogLevel.Debug
            ? new[] { "Debug", "Information", "Warning", "Error" }
            : new[] { "Error" };

        provider
            .Loggers["Microsoft.Hosting"]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(expected);
    }

    /// <summary>
    ///     Lifecycle loggers use the Host category and invoke their callback once.
    /// </summary>
    [Test]
    public void CreateHostLogger_UsesHostCategoryAndConfiguredProvider()
    {
        var provider = new RecordingLoggerProvider();
        var callbackCount = 0;

        using var logger = LogUtil.CreateHostLogger(builder =>
        {
            callbackCount++;
            builder.Services.AddSingleton<ILoggerProvider>(_ => provider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        logger.LogDebug("Starting host");

        callbackCount.ShouldBe(1);
        provider.Loggers.Keys.ShouldBe(["Host"]);

        provider
            .Loggers["Host"]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(["Starting host"]);
    }

    /// <summary>
    ///     Disposing either helper's result disposes providers created by its service container.
    /// </summary>
    /// <param name="useHostLogger">Whether the factory is owned by a HostLogger wrapper.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void Disposal_DisposesFactoryOwnedProviders(bool useHostLogger)
    {
        var provider = new RecordingLoggerProvider();
        Action<ILoggingBuilder> configure = builder => builder.Services.AddSingleton<ILoggerProvider>(_ => provider);
        IDisposable owner;
        ILogger logger;

        if (useHostLogger)
        {
            var hostLogger = LogUtil.CreateHostLogger(configure);
            owner = hostLogger;
            logger = hostLogger;
        }
        else
        {
            var factory = LogUtil.CreateHostLoggerFactory(configure);
            owner = factory;
            logger = factory.CreateLogger("Bootstrap");
        }

        using (owner)
        {
            logger.LogInformation("Before disposal");
            provider.DisposeCount.ShouldBe(0);

            provider
                .Loggers
                .Values
                .Single()
                .Entries
                .Count
                .ShouldBe(1);
        }

        provider.DisposeCount.ShouldBe(1);
    }

    /// <summary>
    ///     Separately created factories do not share configured providers or filters.
    /// </summary>
    [Test]
    public void Factories_HaveIndependentProvidersAndConfiguration()
    {
        var firstProvider = new RecordingLoggerProvider();
        var secondProvider = new RecordingLoggerProvider();

        using var firstFactory = LogUtil.CreateHostLoggerFactory(builder =>
        {
            builder.Services.AddSingleton<ILoggerProvider>(_ => firstProvider);
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        using var secondFactory = LogUtil.CreateHostLoggerFactory(builder =>
        {
            builder.Services.AddSingleton<ILoggerProvider>(_ => secondProvider);
            builder.SetMinimumLevel(LogLevel.Error);
        });

        firstFactory
            .CreateLogger("Bootstrap")
            .LogDebug("First factory");

        secondFactory
            .CreateLogger("Bootstrap")
            .LogDebug("Filtered");

        secondFactory
            .CreateLogger("Bootstrap")
            .LogError("Second factory");

        firstProvider
            .Loggers["Bootstrap"]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(["First factory"]);

        secondProvider
            .Loggers["Bootstrap"]
            .Entries
            .Select(entry => entry.Message)
            .ShouldBe(["Second factory"]);
    }
}
