namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Covers scope helper delegation, provider integration, and capture of buffered routing identities.
/// </summary>
public sealed class GroupScopeTests
{
    /// <summary>
    ///     The helper supplies exactly one structured property and returns the original scope token.
    /// </summary>
    /// <param name="group">The value supplied to the helper.</param>
    [TestCase("Orders")]
    [TestCase("")]
    [TestCase(null)]
    public void BeginGroupScope_DelegatesStateAndReturnsUnderlyingToken(string? group)
    {
        var token = A.Fake<IDisposable>();
        var logger = new ScopeRecordingLogger(token);

        using (var scope = logger.BeginGroupScope(group!))
        {
            scope.ShouldBeSameAs(token);
            var properties = logger.State.ShouldBeAssignableTo<IEnumerable<KeyValuePair<string, object?>>>();
            properties.ShouldBe([new("Group", group)]);
        }

        A
            .CallTo(() => token.Dispose())
            .MustHaveHappenedOnceExactly();
    }

    /// <summary>
    ///     The helper preserves a logger's null scope return value.
    /// </summary>
    [Test]
    public void BeginGroupScope_PreservesNullToken()
    {
        var logger = new ScopeRecordingLogger(null);

        logger
            .BeginGroupScope("Orders")
            .ShouldBeNull();
    }

    /// <summary>
    ///     A null receiver reports the public parameter name.
    /// </summary>
    [Test]
    public void BeginGroupScope_RejectsNullLogger()
    {
        ILogger logger = null!;

        Should
            .Throw<ArgumentNullException>(() => logger.BeginGroupScope("Orders"))
            .ParamName
            .ShouldBe("logger");
    }

    /// <summary>
    ///     Cached loggers observe a replacement external scope provider and support fallback scopes.
    /// </summary>
    /// <param name="buffered">Whether to use the background writer.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void Provider_CachedLoggerUsesReplacementExternalScopeProvider(bool buffered)
    {
        var fileSystem = new MockFileSystem();
        var timeProvider = new TestTimeProvider();
        BufferedFileLoggerProvider.FileSystem = DirectFileLoggerProvider.FileSystem = fileSystem;
        BufferedFileLoggerProvider.TimeProvider = DirectFileLoggerProvider.TimeProvider = timeProvider;

        var config = new FileLoggerConfiguration
        {
            LogName = "app",
            PerGroupLogName = new()
            {
                ["Orders"] = "orders",
                ["Billing"] = "billing",
            },
        };

        var options = A.Fake<IOptionsMonitor<FileLoggerConfiguration>>();

        A
            .CallTo(() => options.CurrentValue)
            .Returns(config);

        using ILoggerProvider provider = buffered
            ? new BufferedFileLoggerProvider(options)
            : new DirectFileLoggerProvider(options);

        var logger = provider.CreateLogger("Cached.Category");

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("Fallback provider scope");

        var externalScopes = new LoggerExternalScopeProvider();
        ((ISupportExternalScope)provider).SetScopeProvider(externalScopes);

        provider
            .CreateLogger("Cached.Category")
            .ShouldBeSameAs(logger);

        using (externalScopes.Push(new Dictionary<string, object?>
               {
                   ["Group"] = "Billing",
               }))
            logger.LogInformation("Replacement provider scope");

        provider.Dispose();

        fileSystem
            .File
            .ReadAllText(GetLogPath(fileSystem, "app_orders"))
            .ShouldContain("Fallback provider scope");

        fileSystem
            .File
            .ReadAllText(GetLogPath(fileSystem, "app_billing"))
            .ShouldContain("Replacement provider scope");

        fileSystem
            .AllFiles
            .Count()
            .ShouldBe(2);
    }

    /// <summary>
    ///     Queued entries keep their group string after the dictionary is mutated and the scope is disposed.
    /// </summary>
    [Test]
    public void BufferedLogger_CapturesGroupBeforeScopeMutationAndDisposal()
    {
        var fileSystem = new MockFileSystem();

        var config = new FileLoggerConfiguration
        {
            LogName = "app",
            PerGroupLogName = new()
            {
                ["Orders"] = "orders",
                ["Billing"] = "billing",
            },
        };

        using var writer = new BufferedFileLogWriter(fileSystem, new TestTimeProvider(), () => config);
        var scopes = new LoggerExternalScopeProvider();
        var logger = new FileLogger("Captured.Category", writer, () => scopes);

        var state = new Dictionary<string, object?>
        {
            ["Group"] = "Orders",
        };

        using (logger.BeginScope(state))
        {
            for (var index = 0; index < 25; index++)
                logger.LogInformation("Order entry {Index}", index);

            state["Group"] = "Billing";
            logger.LogInformation("Billing after mutation");
        }

        logger.LogInformation("Outside scope");
        writer.Start();
        writer.Dispose();

        var lines = fileSystem.File.ReadAllLines(GetLogPath(fileSystem, "app_orders"));
        lines.Length.ShouldBe(25);

        for (var index = 0; index < lines.Length; index++)
            lines[index]
                .ShouldEndWith($"Order entry {index}");

        fileSystem
            .File
            .ReadAllText(GetLogPath(fileSystem, "app_billing"))
            .ShouldContain("Billing after mutation");

        fileSystem
            .File
            .ReadAllText(GetLogPath(fileSystem, "app"))
            .ShouldContain("Outside scope");
    }

    /// <summary>
    ///     Group identities are captured even before a mapping exists, then resolved from write-time configuration.
    /// </summary>
    [Test]
    public void BufferedLogger_ResolvesQueuedGroupsUsingConfigurationAtWriteTime()
    {
        var fileSystem = new MockFileSystem();

        var config = new FileLoggerConfiguration
        {
            LogName = "old-app",
        };

        using var writer = new BufferedFileLogWriter(fileSystem, new TestTimeProvider(), () => config);
        var scopes = new LoggerExternalScopeProvider();
        var logger = new FileLogger("Reload.Category", writer, () => scopes);

        using (logger.BeginGroupScope("Orders"))
            logger.LogError("Queued before group configured");

        config = new()
        {
            LogName = "new-app",
            PerGroupLogName = new()
            {
                ["Orders"] = "new-orders",
            },
            PerLevelLogName = new()
            {
                [LogLevel.Error] = "new-errors",
            },
        };

        writer.Start();
        writer.Dispose();

        var expectedPath = GetLogPath(fileSystem, "new-app_new-orders_new-errors");
        fileSystem.AllFiles.ShouldBe([expectedPath]);

        fileSystem
            .File
            .ReadAllText(expectedPath)
            .ShouldContain("Queued before group configured");
    }

    /// <summary>
    ///     Group aliases preserve same-level enqueue order across batches, respecting filesystem case sensitivity.
    /// </summary>
    /// <param name="billingName">The second group's destination, including a possible case alias.</param>
    /// <param name="logName">The base filename, including a possible current-directory prefix.</param>
    [TestCase("shared", "app")]
    [TestCase("SHARED", "app")]
    [TestCase("shared", "./app")]
    public void BufferedLogger_PreservesOrderForGroupsSharingDestination(string billingName, string logName)
    {
        var fileSystem = new MockFileSystem();

        var config = new FileLoggerConfiguration
        {
            LogName = logName,
            PerGroupLogName = new()
            {
                ["Orders"] = "shared",
                ["Billing"] = billingName,
            },
        };

        using var writer = new BufferedFileLogWriter(fileSystem, new TestTimeProvider(), () => config);
        var scopes = new LoggerExternalScopeProvider();
        var logger = new FileLogger("Shared.Category", writer, () => scopes);

        for (var index = 0; index < 25; index++)
            using (logger.BeginGroupScope(index % 2 == 0
                       ? "Orders"
                       : "Billing"))
                logger.LogInformation("Shared entry {Index}", index);

        writer.Start();
        writer.Dispose();

        var lines = fileSystem.File.ReadAllLines(GetLogPath(fileSystem, "app_shared"));

        if (billingName == "shared" || fileSystem.Path.DirectorySeparatorChar == '\\')
        {
            fileSystem
                .AllFiles
                .Count()
                .ShouldBe(1);

            lines.Length.ShouldBe(25);

            for (var index = 0; index < lines.Length; index++)
                lines[index]
                    .ShouldEndWith($"Shared entry {index}");
        }
        else
        {
            fileSystem
                .AllFiles
                .Count()
                .ShouldBe(2);

            lines.Length.ShouldBe(13);

            for (var index = 0; index < lines.Length; index++)
                lines[index]
                    .ShouldEndWith($"Shared entry {index * 2}");

            var billingLines = fileSystem.File.ReadAllLines(GetLogPath(fileSystem, $"app_{billingName}"));
            billingLines.Length.ShouldBe(12);

            for (var index = 0; index < billingLines.Length; index++)
                billingLines[index]
                    .ShouldEndWith($"Shared entry {index * 2 + 1}");
        }
    }

    /// <summary>
    ///     Resolves an active log path entirely through the mock file system.
    /// </summary>
    /// <param name="fileSystem">The test file system.</param>
    /// <param name="name">The destination's base name.</param>
    /// <returns>The active log's full path.</returns>
    private static string GetLogPath(MockFileSystem fileSystem, string name) =>
        fileSystem.Path.Combine(fileSystem.Directory.GetCurrentDirectory(), "Logs", $"{name}.log");

    /// <summary>
    ///     Records a helper's scope state and returns a caller-selected disposable.
    /// </summary>
    /// <param name="token">The scope token to return, including null.</param>
    private sealed class ScopeRecordingLogger(IDisposable? token) : ILogger
    {
        /// <summary>
        ///     Gets the most recently supplied scope state.
        /// </summary>
        public object? State { get; private set; }

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            State = state;

            return token;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) =>
            true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) { }
    }
}
