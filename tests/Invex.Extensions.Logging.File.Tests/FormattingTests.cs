namespace Invex.Extensions.Logging.File.Tests;

public sealed class FormattingTests : TestBase
{
    [SetUp]
    public void SetUp()
    {
        FileSystem = new();
        BufferedFileLoggerProvider.FileSystem = FileSystem;

        TimeProvider = new();
        BufferedFileLoggerProvider.TimeProvider = TimeProvider;
    }

    [Test]
    public void Logger_NoFormatting_LogsDefaultFormat()
    {
        // Arrange
        var logPath = GetLogPath();
        var logger = CreateBuilderWithLogger<FormattingTests>();

        var logTimestamp = TimeProvider
            .GetLocalNow()
            .ToString("yyyy-MM-dd HH:mm:ss.fff zzz");

        const string logLevelContext = "Invex.Extensions.Logging.File.Tests.FormattingTests";

        // Act
        logger.LogTrace("Hello, world 1!");
        logger.LogDebug("Hello, world 2!");
        logger.LogInformation("Hello, world 3!");
        logger.LogWarning("Hello, world 4!");
        logger.LogError("Hello, world 5!");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs.File.Exists(logPath),
            fs => fs
                .File
                .ReadAllText(logPath)
                .ShouldBe($"""
                           [{logTimestamp} TRC {logLevelContext}] Hello, world 1!
                           [{logTimestamp} DBG {logLevelContext}] Hello, world 2!
                           [{logTimestamp} INF {logLevelContext}] Hello, world 3!
                           [{logTimestamp} WRN {logLevelContext}] Hello, world 4!
                           [{logTimestamp} ERR {logLevelContext}] Hello, world 5!

                           """));

        TestContext.Out.WriteLine($"Log file: {logPath}");
        TestContext.Out.WriteLine();
        TestContext.Out.WriteLine($"Log content:\n{FileSystem.File.ReadAllText(logPath)}");
    }
}
