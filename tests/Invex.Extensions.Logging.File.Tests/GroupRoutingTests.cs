using Invex.Extensions.Logging.File.Configuration;
using Invex.Extensions.Logging.File.Provider;

namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Verifies that scoped group routing behaves identically for both writer modes.
/// </summary>
/// <param name="buffered">Whether entries use the background writer.</param>
[TestFixture(true)]
[TestFixture(false)]
public sealed class GroupRoutingTests(bool buffered) : TestBase
{
    /// <summary>
    ///     Replaces both providers' file systems and clocks with deterministic test implementations.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        FileSystem = new();
        TimeProvider = new();
        BufferedFileLoggerProvider.FileSystem = DirectFileLoggerProvider.FileSystem = FileSystem;
        BufferedFileLoggerProvider.TimeProvider = DirectFileLoggerProvider.TimeProvider = TimeProvider;
    }

    /// <summary>
    ///     Flushes and disposes the host even if an assertion fails.
    /// </summary>
    [TearDown]
    public void TearDown() =>
        StopApp(false);

    /// <summary>
    ///     Applies a group mapping before composing the optional level suffix.
    /// </summary>
    /// <param name="group">The scoped group, if present.</param>
    /// <param name="level">The entry's severity.</param>
    /// <param name="expectedName">The expected destination's base name.</param>
    [TestCase("Orders", LogLevel.Error, "app_orders_errors")]
    [TestCase("Orders", LogLevel.Information, "app_orders")]
    [TestCase(null, LogLevel.Error, "app_errors")]
    [TestCase(null, LogLevel.Information, "app")]
    [TestCase("Unknown", LogLevel.Error, "app_errors")]
    [TestCase("Unknown", LogLevel.Information, "app")]
    [TestCase("orders", LogLevel.Information, "app")]
    public void Logger_RoutesByGroupThenLevel(string? group, LogLevel level, string expectedName)
    {
        var logger = CreateLogger();

        using (group is null
                   ? null
                   : logger.BeginGroupScope(group))
            logger.Log(level, "Routed entry");

        StopApp(false);

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: expectedName)]);

        ReadLog(expectedName)
            .ShouldContain("Routed entry");
    }

    /// <summary>
    ///     Null and empty mappings omit their suffix and separator while retaining the configured or default base.
    /// </summary>
    /// <param name="logName">The configured base filename, or null to use the application name.</param>
    /// <param name="groupSuffix">The mapped group suffix.</param>
    /// <param name="levelSuffix">The mapped level suffix.</param>
    /// <param name="expectedSuffix">The expected suffixes including their separators.</param>
    [TestCase("app", null, "errors", "_errors")]
    [TestCase("app", "", "errors", "_errors")]
    [TestCase("app", "orders", null, "_orders")]
    [TestCase("app", "orders", "", "_orders")]
    [TestCase("app", null, null, "")]
    [TestCase("app", "", "", "")]
    [TestCase("app", null, "", "")]
    [TestCase("app", "", null, "")]
    [TestCase(null, null, "errors", "_errors")]
    [TestCase(null, "orders", "", "_orders")]
    [TestCase(null, null, null, "")]
    [TestCase(null, "", "", "")]
    public void Logger_OmitsNullAndEmptySuffixes(
        string? logName,
        string? groupSuffix,
        string? levelSuffix,
        string expectedSuffix)
    {
        var logger = CreateBuilderWithLogger<GroupRoutingTests>(config =>
            {
                config.LogName = logName;
                config.PerGroupLogName["Orders"] = groupSuffix;
                config.PerLevelLogName[LogLevel.Error] = levelSuffix;
            },
            buffered);

        using (logger.BeginGroupScope("Orders"))
            logger.LogError("Optional suffix entry");

        StopApp(false);

        var expectedName = $"{logName ?? AppDomain.CurrentDomain.FriendlyName}{expectedSuffix}";
        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: expectedName)]);

        ReadLog(expectedName)
            .ShouldContain("Optional suffix entry");
    }

    /// <summary>
    ///     An empty group dictionary preserves default application-name routing.
    /// </summary>
    [Test]
    public void Logger_WithDefaultConfiguration_IgnoresUnmappedScope()
    {
        new FileLoggerConfiguration().PerGroupLogName.ShouldBeEmpty();
        var logger = CreateBuilderWithLogger<GroupRoutingTests>(buffered: buffered);

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("Default route");

        StopApp(false);

        FileSystem.AllFiles.ShouldBe([GetLogPath()]);
    }

    /// <summary>
    ///     A group suffix extends the default application name, followed by any matching level suffix.
    /// </summary>
    /// <param name="level">The entry's severity.</param>
    /// <param name="expectedSuffix">The suffix appended to the application name.</param>
    [TestCase(LogLevel.Information, "_MyGroup")]
    [TestCase(LogLevel.Error, "_MyGroup_ERR")]
    public void Logger_AppendsMyGroupToDefaultBaseName(LogLevel level, string expectedSuffix)
    {
        var logger = CreateBuilderWithLogger<GroupRoutingTests>(config =>
            {
                config.PerGroupLogName["MyGroup"] = "MyGroup";
                config.PerLevelLogName[LogLevel.Error] = "ERR";
            },
            buffered);

        using (logger.BeginGroupScope("MyGroup"))
            logger.Log(level, "Default base with suffixes");

        StopApp(false);

        var expectedName = $"{AppDomain.CurrentDomain.FriendlyName}{expectedSuffix}";
        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: expectedName)]);

        ReadLog(expectedName)
            .ShouldContain("Default base with suffixes");
    }

    /// <summary>
    ///     A null or empty group-only mapping preserves the configured base filename.
    /// </summary>
    /// <param name="groupSuffix">The omitted group suffix.</param>
    [TestCase(null)]
    [TestCase("")]
    public void Logger_NullOrEmptyGroupMappingWithoutLevelMapping_UsesBaseName(string? groupSuffix)
    {
        var logger = CreateBuilderWithLogger<GroupRoutingTests>(config =>
            {
                config.LogName = "app";
                config.PerGroupLogName["Orders"] = groupSuffix;
            },
            buffered);

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("Null group-only mapping");

        StopApp(false);

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: "app")]);

        FileSystem
            .File
            .ReadAllText(GetLogPath(customName: "app"))
            .ShouldContain("Null group-only mapping");
    }

    /// <summary>
    ///     Group lookup respects a custom dictionary comparer.
    /// </summary>
    [Test]
    public void Logger_UsesConfiguredGroupComparer()
    {
        var logger = CreateBuilderWithLogger<GroupRoutingTests>(config =>
            {
                config.LogName = "app";

                config.PerGroupLogName = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Orders"] = "orders",
                };
            },
            buffered);

        using (logger.BeginGroupScope("ORDERS"))
            logger.LogInformation("Case insensitive entry");

        StopApp(false);

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: "app_orders")]);
    }

    /// <summary>
    ///     Nested scopes restore outer routing, while a valid unmapped group overrides the outer group.
    /// </summary>
    [Test]
    public void Logger_NestedScopes_RestoreOuterGroupAfterDisposal()
    {
        var logger = CreateLogger();

        using (logger.BeginGroupScope("Orders"))
        {
            logger.LogInformation("Outer before");

            using (logger.BeginGroupScope("Billing"))
                logger.LogInformation("Inner billing");

            using (logger.BeginGroupScope("Unmapped"))
                logger.LogError("Inner unmapped");

            logger.LogInformation("Outer after");
        }

        logger.LogInformation("Outside scopes");
        StopApp(false);

        ReadLog("app_orders")
            .ShouldContain("Outer before");

        ReadLog("app_orders")
            .ShouldContain("Outer after");

        ReadLog("app_orders")
            .ShouldNotContain("Inner");

        ReadLog("app_billing")
            .ShouldContain("Inner billing");

        ReadLog("app_errors")
            .ShouldContain("Inner unmapped");

        ReadLog("app")
            .ShouldContain("Outside scopes");
    }

    /// <summary>
    ///     Only a nonempty string under the exact structured Group key replaces an outer group.
    /// </summary>
    [Test]
    public void Logger_IgnoresInvalidAndUnrelatedInnerScopes()
    {
        var logger = CreateLogger();

        object[] invalidStates =
        [
            new Dictionary<string, object?>
            {
                ["Group"] = null,
            },
            new Dictionary<string, object?>
            {
                ["Group"] = "",
            },
            new Dictionary<string, object?>
            {
                ["Group"] = 42,
            },
            new Dictionary<string, object?>
            {
                ["group"] = "Billing",
            },
            new Dictionary<string, object?>
            {
                ["Other"] = "Billing",
            },
            "Billing",
        ];

        using (logger.BeginGroupScope("Orders"))
        {
            for (var index = 0; index < invalidStates.Length; index++)
                using (logger.BeginScope(invalidStates[index]))
                    logger.LogInformation("Ignored scope {Index}", index);
        }

        StopApp(false);

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: "app_orders")]);

        for (var index = 0; index < invalidStates.Length; index++)
            ReadLog("app_orders")
                .ShouldContain($"Ignored scope {index}");
    }

    /// <summary>
    ///     Helpers, dictionaries, and formatted scopes supply the same routing value without changing log text.
    /// </summary>
    [Test]
    public void Logger_AcceptsHelperDictionaryAndFormattedGroupScopes()
    {
        var logger = CreateLogger();

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("Same entry");

        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["Group"] = "Orders",
               }))
            logger.LogInformation("Same entry");

        using (logger.BeginScope("{Group}", "Orders"))
            logger.LogInformation("Same entry");

        StopApp(false);

        var lines = FileSystem.File.ReadAllLines(GetLogPath(customName: "app_orders"));
        lines.Length.ShouldBe(3);

        lines
            .Distinct()
            .Count()
            .ShouldBe(1);

        lines[0]
            .ShouldBe(
                "[2020-01-01 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.GroupRoutingTests] Same entry");
    }

    /// <summary>
    ///     A scope enumeration failure drops its entry without throwing or rerouting, then logging recovers.
    /// </summary>
    [Test]
    public void Logger_ThrowingStructuredScope_DropsEntryAndContinues()
    {
        var logger = CreateLogger();
        var properties = A.Fake<IEnumerable<KeyValuePair<string, object?>>>();

        A
            .CallTo(() => properties.GetEnumerator())
            .Throws<InvalidOperationException>();

        using (logger.BeginGroupScope("Orders"))
        {
            using (logger.BeginScope(properties))
                Should.NotThrow(() => logger.LogInformation("Unresolvable entry"));

            logger.LogInformation("Healthy entry");
        }

        StopApp(false);

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: "app_orders")]);

        ReadLog("app_orders")
            .ShouldContain("Healthy entry");

        ReadLog("app_orders")
            .ShouldNotContain("Unresolvable entry");
    }

    /// <summary>
    ///     External scopes flow across asynchronous work and logger categories without leaking to siblings.
    /// </summary>
    [Test]
    public async Task Logger_GroupFlowsAcrossAwaitAndCategories_WithConcurrentIsolation()
    {
        using var factory = LoggerFactory.Create(builder => builder.AddFile(Configure, buffered));
        var first = factory.CreateLogger("First.Category");
        var second = factory.CreateLogger("Second.Category");
        var bothStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = 0;

        async Task LogGroup(string group)
        {
            using (first.BeginGroupScope(group))
            {
                if (Interlocked.Increment(ref started) == 2)
                    bothStarted.SetResult(true);

                await bothStarted.Task;
                await Task.Yield();
                second.LogInformation("Concurrent {GroupName}", group);
            }
        }

        await Task.WhenAll(LogGroup("Orders"), LogGroup("Billing"));
        second.LogInformation("Outside concurrent scopes");
        factory.Dispose();

        ReadLog("app_orders")
            .ShouldContain("Second.Category] Concurrent Orders");

        ReadLog("app_orders")
            .ShouldNotContain("Billing");

        ReadLog("app_billing")
            .ShouldContain("Second.Category] Concurrent Billing");

        ReadLog("app_billing")
            .ShouldNotContain("Orders");

        ReadLog("app")
            .ShouldContain("Outside concurrent scopes");
    }

    /// <summary>
    ///     The Logging:File section binds group mappings from JSON configuration.
    /// </summary>
    [Test]
    public void Logger_BindsGroupAndLevelMappingsFromJson()
    {
        const string json = """
                            {
                              "Logging": {
                                "File": {
                                  "LogName": "app",
                                  "PerGroupLogName": { "Orders": "orders" },
                                  "PerLevelLogName": { "Error": "errors" }
                                }
                              }
                            }
                            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonStream(stream);
        builder.Logging.ClearProviders();
        builder.Logging.AddFile(buffered);
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<GroupRoutingTests>>();

        using (logger.BeginGroupScope("Orders"))
            logger.LogError("Bound from JSON");

        host.Dispose();

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: "app_orders_errors")]);

        ReadLog("app_orders_errors")
            .ShouldContain("Bound from JSON");
    }

    /// <summary>
    ///     Existing loggers and scopes observe mappings added through configuration reload.
    /// </summary>
    [Test]
    public void Logger_UsesReloadedGroupAndLevelMappings()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:File:LogName"] = "app",
            ["Logging:File:PerLevelLogName:Error"] = "old-errors",
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddFile(buffered);
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<GroupRoutingTests>>();

        using (logger.BeginGroupScope("Orders"))
        {
            builder.Configuration["Logging:File:PerGroupLogName:Orders"] = "new-orders";
            builder.Configuration["Logging:File:PerLevelLogName:Error"] = "new-errors";
            ((IConfigurationRoot)builder.Configuration).Reload();

            host
                .Services
                .GetRequiredService<IOptionsMonitor<FileLoggerConfiguration>>()
                .CurrentValue
                .PerGroupLogName["Orders"]
                .ShouldBe("new-orders");

            logger.LogError("Reloaded destination");
        }

        host.Dispose();

        FileSystem.AllFiles.ShouldBe([GetLogPath(customName: "app_new-orders_new-errors")]);

        ReadLog("app_new-orders_new-errors")
            .ShouldContain("Reloaded destination");
    }

    /// <summary>
    ///     Creates a logger with the shared group and level routing matrix.
    /// </summary>
    /// <returns>The host-managed logger.</returns>
    private ILogger CreateLogger() =>
        CreateBuilderWithLogger<GroupRoutingTests>(Configure, buffered);

    /// <summary>
    ///     Configures two group destinations and one level destination.
    /// </summary>
    /// <param name="config">The configuration to initialize.</param>
    private static void Configure(FileLoggerConfiguration config)
    {
        config.LogName = "app";
        config.PerGroupLogName["Orders"] = "orders";
        config.PerGroupLogName["Billing"] = "billing";
        config.PerLevelLogName[LogLevel.Error] = "errors";
    }

    /// <summary>
    ///     Reads a named log from the mock file system.
    /// </summary>
    /// <param name="name">The destination's base name.</param>
    /// <returns>The complete log content.</returns>
    private string ReadLog(string name) =>
        FileSystem.File.ReadAllText(GetLogPath(customName: name));
}
