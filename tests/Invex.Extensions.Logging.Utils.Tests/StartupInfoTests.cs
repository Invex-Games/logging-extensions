namespace Invex.Extensions.Logging.Utils.Tests;

/// <summary>
///     Covers startup message formatting, structured state, assembly metadata, and filtering.
/// </summary>
[TestFixture]
public sealed class StartupInfoTests
{
    /// <summary>
    ///     Startup messages retain the documented structured template and display clauses.
    /// </summary>
    [Test]
    public void ExplicitValues_PreserveStructuredState()
    {
        var logger = new RecordingLogger();

        LogUtil.LogStartupInfo(logger, "OrderWorker", "v2.3.0", "worker-01", "Production");

        logger.EnabledChecks.ShouldBe([LogLevel.Information]);
        logger.Entries.Count.ShouldBe(1);
        var entry = logger.Entries[0];
        entry.Level.ShouldBe(LogLevel.Information);
        entry.EventId.ShouldBe(default);
        entry.Exception.ShouldBeNull();
        entry.Message.ShouldBe("Started OrderWorker v2.3.0 on worker-01 in Production configuration");
        var properties = entry.State.ShouldBeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>();

        properties.ShouldBe([
            new("AppName", " OrderWorker"),
            new("Version", " v2.3.0"),
            new("MachineName", " on worker-01"),
            new("Environment", " in Production configuration"),
            new("{OriginalFormat}", "Started{AppName}{Version}{MachineName}{Environment}"),
        ]);
    }

    /// <summary>
    ///     Null and empty values omit optional clauses and use the fallback application name.
    /// </summary>
    /// <param name="value">The absent value supplied for each input.</param>
    [TestCase(null)]
    [TestCase("")]
    public void AbsentValues_UseApplicationFallback(string? value)
    {
        var logger = new RecordingLogger();

        LogUtil.LogStartupInfo(logger, value, value, value, value);

        logger.Entries.Count.ShouldBe(1);

        logger
            .Entries[0]
            .Message
            .ShouldBe("Started Application");
    }

    /// <summary>
    ///     Each optional clause can be omitted independently without affecting the others.
    /// </summary>
    /// <param name="application">The application name.</param>
    /// <param name="version">The version.</param>
    /// <param name="machine">The machine name.</param>
    /// <param name="environment">The environment name.</param>
    /// <param name="expected">The expected message.</param>
    [TestCase(null, "1.0", "node", "Production", "Started Application v1.0 on node in Production configuration")]
    [TestCase("App", null, "node", "Production", "Started App on node in Production configuration")]
    [TestCase("App", "", "node", "Production", "Started App on node in Production configuration")]
    [TestCase("App", "1.0", null, "Production", "Started App v1.0 in Production configuration")]
    [TestCase("App", "1.0", "", "Production", "Started App v1.0 in Production configuration")]
    [TestCase("App", "1.0", "node", null, "Started App v1.0 on node")]
    [TestCase("App", "1.0", "node", "", "Started App v1.0 on node")]
    public void OptionalClauses_AreOmittedIndependently(
        string? application,
        string? version,
        string? machine,
        string? environment,
        string expected)
    {
        var logger = new RecordingLogger();

        LogUtil.LogStartupInfo(logger, application, version, machine, environment);

        logger.Entries.Count.ShouldBe(1);

        logger
            .Entries[0]
            .Message
            .ShouldBe(expected);
    }

    /// <summary>
    ///     Only leading version prefix characters are removed, and whitespace is retained.
    /// </summary>
    /// <param name="version">The supplied version.</param>
    /// <param name="expected">The expected message.</param>
    [TestCase("1.2.3", "Started App v1.2.3")]
    [TestCase("v1.2.3", "Started App v1.2.3")]
    [TestCase("V1.2.3", "Started App v1.2.3")]
    [TestCase("vVv1.2.3", "Started App v1.2.3")]
    [TestCase("vV", "Started App v")]
    [TestCase("1v2", "Started App v1v2")]
    [TestCase(" v1.2.3 ", "Started App v v1.2.3 ")]
    public void Version_NormalizesOnlyLeadingPrefixes(string version, string expected)
    {
        var logger = new RecordingLogger();

        LogUtil.LogStartupInfo(logger, "App", version, null, null);

        logger
            .Entries[0]
            .Message
            .ShouldBe(expected);
    }

    /// <summary>
    ///     Whitespace-only inputs are displayed rather than treated as missing.
    /// </summary>
    [Test]
    public void WhitespaceOnlyValues_AreRetained()
    {
        var logger = new RecordingLogger();

        LogUtil.LogStartupInfo(logger, " ", " ", " ", " ");

        logger
            .Entries[0]
            .Message
            .ShouldBe("Started Application");
    }

    /// <summary>
    ///     Both overloads skip entries when Information is disabled.
    /// </summary>
    /// <param name="generic">Whether to use the assembly-based overload.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void InformationDisabled_SkipsEntry(bool generic)
    {
        var logger = new RecordingLogger
        {
            Enabled = false,
        };

        if (generic)
            LogUtil.LogStartupInfo<StartupInfoTests>(logger);
        else
            LogUtil.LogStartupInfo(logger, "App", "1.0", "node", "Production");

        logger.EnabledChecks.ShouldBe([LogLevel.Information]);
        logger.Entries.ShouldBeEmpty();
    }

    /// <summary>
    ///     The generic overload uses the selected assembly and the host environment's environment name.
    /// </summary>
    /// <param name="includeEnvironment">Whether a host environment is provided.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void GenericOverload_UsesSelectedAssemblyAndMachine(bool includeEnvironment)
    {
        var logger = new RecordingLogger();
        var environment = A.Fake<IHostEnvironment>();

        A
            .CallTo(() => environment.EnvironmentName)
            .Returns("Staging");

        A
            .CallTo(() => environment.ApplicationName)
            .Returns("NotTheAssemblyName");

        LogUtil.LogStartupInfo<HostLogger>(logger,
            includeEnvironment
                ? environment
                : null);

        var assemblyName = typeof(HostLogger).Assembly.GetName();

        var environmentClause = includeEnvironment
            ? " in Staging configuration"
            : string.Empty;

        logger.EnabledChecks.ShouldBe([LogLevel.Information]);
        logger.Entries.Count.ShouldBe(1);

        logger
            .Entries[0]
            .Level
            .ShouldBe(LogLevel.Information);

        logger
            .Entries[0]
            .Message
            .ShouldBe(
                $"Started {assemblyName.Name} v{assemblyName.Version} on {Environment.MachineName}{environmentClause}");

        A
            .CallTo(() => environment.ApplicationName)
            .MustNotHaveHappened();
    }

    /// <summary>
    ///     Failures from a custom logger remain visible to the caller.
    /// </summary>
    [Test]
    public void LoggingFailure_Propagates()
    {
        var logger = A.Fake<ILogger>();
        var failure = new InvalidOperationException("Logging failed");

        A
            .CallTo(() => logger.IsEnabled(LogLevel.Information))
            .Returns(true);

        A
            .CallTo(logger)
            .Where(call => call.Method.Name == nameof(ILogger.Log))
            .Throws(failure);

        Should
            .Throw<InvalidOperationException>(() => LogUtil.LogStartupInfo(logger, "App", "1.0", "node", "Production"))
            .ShouldBeSameAs(failure);
    }
}
