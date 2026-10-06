namespace Invex.Extensions.Logging.Utils.Tests;

/// <summary>
///     Covers wrapper delegation, startup information, and factory lifetime ownership.
/// </summary>
[TestFixture]
public sealed class HostLoggerTests
{
    /// <summary>
    ///     Logging preserves the exact state, exception, formatter, event, and level without an enabled check.
    /// </summary>
    [Test]
    public void Log_ForwardsEveryArgumentWithoutFiltering()
    {
        var logger = A.Fake<ILogger>();
        var factory = A.Fake<ILoggerFactory>();
        using var hostLogger = new HostLogger(logger, factory);
        var state = new object();
        var exception = new InvalidOperationException("Boom");
        var eventId = new EventId(42, "Lifecycle");
        Func<object, Exception?, string> formatter = (_, _) => "Formatted";

        hostLogger.Log(LogLevel.Debug, eventId, state, exception, formatter);

        A
            .CallTo(() => logger.Log(LogLevel.Debug, eventId, state, exception, formatter))
            .MustHaveHappenedOnceExactly();

        A
            .CallTo(() => logger.IsEnabled(A<LogLevel>._))
            .MustNotHaveHappened();
    }

    /// <summary>
    ///     Enabled checks return the wrapped logger's result for the supplied level.
    /// </summary>
    /// <param name="level">The queried level.</param>
    /// <param name="enabled">The wrapped logger's enabled state.</param>
    [TestCase(LogLevel.Debug, true)]
    [TestCase(LogLevel.Warning, false)]
    [TestCase(LogLevel.None, true)]
    public void IsEnabled_DelegatesResult(LogLevel level, bool enabled)
    {
        var logger = A.Fake<ILogger>();
        using var hostLogger = new HostLogger(logger, A.Fake<ILoggerFactory>());

        A
            .CallTo(() => logger.IsEnabled(level))
            .Returns(enabled);

        hostLogger
            .IsEnabled(level)
            .ShouldBe(enabled);

        A
            .CallTo(() => logger.IsEnabled(level))
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>
    ///     Scopes preserve their state and return the original token, including null.
    /// </summary>
    /// <param name="returnToken">Whether the wrapped logger supplies a token.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void BeginScope_ForwardsStateAndToken(bool returnToken)
    {
        var logger = A.Fake<ILogger>();
        using var hostLogger = new HostLogger(logger, A.Fake<ILoggerFactory>());
        var state = new object();

        var token = returnToken
            ? A.Fake<IDisposable>()
            : null;

        A
            .CallTo(() => logger.BeginScope(state))
            .Returns(token);

        using (var scope = hostLogger.BeginScope(state))
            scope.ShouldBeSameAs(token);

        A
            .CallTo(() => logger.BeginScope(state))
            .MustHaveHappenedOnceExactly();

        if (token is not null)
            A
                .CallTo(() => token.Dispose())
                .MustHaveHappenedOnceExactly();
    }

    /// <summary>
    ///     Disposing the wrapper disposes the supplied factory without disposing the wrapped logger separately.
    /// </summary>
    [Test]
    public void Dispose_DisposesOwnedFactory()
    {
        var logger = A.Fake<ILogger>(options => options.Implements<IDisposable>());
        var factory = A.Fake<ILoggerFactory>();
        var hostLogger = new HostLogger(logger, factory);

        hostLogger.Dispose();

        A
            .CallTo(() => factory.Dispose())
            .MustHaveHappenedOnceExactly();

        A
            .CallTo(() => ((IDisposable)logger).Dispose())
            .MustNotHaveHappened();
    }

    /// <summary>
    ///     Wrapper startup information uses the caller-selected assembly and respects enabled state.
    /// </summary>
    /// <param name="enabled">Whether Information is enabled.</param>
    /// <param name="includeEnvironment">Whether an environment is supplied.</param>
    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public void LogStartupInfo_DelegatesToWrappedLogger(bool enabled, bool includeEnvironment)
    {
        var logger = new RecordingLogger
        {
            Enabled = enabled,
        };

        using var hostLogger = new HostLogger(logger, A.Fake<ILoggerFactory>());
        var environment = A.Fake<IHostEnvironment>();

        A
            .CallTo(() => environment.EnvironmentName)
            .Returns("Production");

        hostLogger.LogStartupInfo<HostLoggerTests>(includeEnvironment
            ? environment
            : null);

        logger.EnabledChecks.ShouldBe([LogLevel.Information]);

        if (!enabled)
        {
            logger.Entries.ShouldBeEmpty();

            return;
        }

        var assemblyName = typeof(HostLoggerTests).Assembly.GetName();

        var environmentClause = includeEnvironment
            ? " in Production configuration"
            : string.Empty;

        logger.Entries.Count.ShouldBe(1);

        logger
            .Entries[0]
            .Level
            .ShouldBe(LogLevel.Information);

        logger
            .Entries[0]
            .Message
            .ShouldBe(
                $"Started {assemblyName.Name} v{assemblyName.Version}{environmentClause} on {Environment.MachineName}");
    }

    /// <summary>
    ///     Failures from wrapped logging operations and disposal propagate to the caller.
    /// </summary>
    /// <param name="operation">The operation that fails.</param>
    [TestCase("Log")]
    [TestCase("IsEnabled")]
    [TestCase("BeginScope")]
    [TestCase("Dispose")]
    public void WrappedFailures_Propagate(string operation)
    {
        var logger = A.Fake<ILogger>();
        var factory = A.Fake<ILoggerFactory>();
        var hostLogger = new HostLogger(logger, factory);
        var failure = new InvalidOperationException("Wrapped operation failed");
        Action action;

        switch (operation)
        {
            case "Log":
                A
                    .CallTo(logger)
                    .Where(call => call.Method.Name == nameof(ILogger.Log))
                    .Throws(failure);

                action = () => hostLogger.LogInformation("Hello");

                break;
            case "IsEnabled":
                A
                    .CallTo(() => logger.IsEnabled(LogLevel.Information))
                    .Throws(failure);

                action = () => hostLogger.IsEnabled(LogLevel.Information);

                break;
            case "BeginScope":
                A
                    .CallTo(() => logger.BeginScope("Scope"))
                    .Throws(failure);

                action = () => hostLogger.BeginScope("Scope");

                break;
            default:
                A
                    .CallTo(() => factory.Dispose())
                    .Throws(failure);

                action = hostLogger.Dispose;

                break;
        }

        Should
            .Throw<InvalidOperationException>(action)
            .ShouldBeSameAs(failure);
    }
}
