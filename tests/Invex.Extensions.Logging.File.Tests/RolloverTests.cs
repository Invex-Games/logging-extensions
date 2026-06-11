namespace Invex.Extensions.Logging.File.Tests;

public sealed class RolloverTests : TestBase
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
    public void Logger_WhenAtFileSizeLimit_RollsLog()
    {
        // Arrange
        const int byteLimit = 1 * 1024 * 1024;
        var logExistingText = $"{new string('a', byteLimit)}\n";

        var logPath = GetLogPath();
        FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(logPath)!);
        FileSystem.File.WriteAllText(logPath, logExistingText);

        var rolledLogPath = GetLogPath(TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss"));

        var logger = CreateBuilderWithLogger<RolloverTests>(c => c.FileSizeLimitBytes = byteLimit);

        // Act
        logger.LogInformation("Hello, world!");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs
                .File
                .Exists(rolledLogPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(rolledLogPath)
                .ShouldBe(logExistingText),
            fs => fs
                .File
                .Exists(logPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(logPath)
                .ShouldBe("""
                          [2020-01-01 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.RolloverTests] Hello, world!

                          """));
    }

    [Test]
    public void Logger_WhenNewLogExceedsFileSizeLimit_RollsLog()
    {
        // Arrange
        const int byteLimit = 1 * 1024 * 1024;

        var logExistingText = $"{new string('a', byteLimit - 2)}\n";

        var logPath = GetLogPath();
        FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(logPath)!);
        FileSystem.File.WriteAllText(logPath, logExistingText);

        var rolledLogPath = GetLogPath(TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss"));

        var logger = CreateBuilderWithLogger<RolloverTests>(c => c.FileSizeLimitBytes = byteLimit);

        // Act
        logger.LogInformation("Hello, world!");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs
                .File
                .Exists(rolledLogPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(rolledLogPath)
                .ShouldBe(logExistingText),
            fs => fs
                .File
                .Exists(logPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(logPath)
                .ShouldBe("""
                          [2020-01-01 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.RolloverTests] Hello, world!

                          """));
    }

    [Test]
    public void Logger_WhenNewLogIsBeyondRollingInterval_RollsLog()
    {
        // Arrange
        const string logExistingText = "\n";

        var logPath = GetLogPath();
        FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(logPath)!);
        FileSystem.File.WriteAllText(logPath, logExistingText);

        FileSystem.File.SetCreationTimeUtc(logPath, TimeProvider.UtcNow.DateTime);

        var logger = CreateBuilderWithLogger<RolloverTests>(c => c.RolloverInterval = FileRolloverInterval.Day);

        TimeProvider.UtcNow = TimeProvider.UtcNow.AddDays(1);

        // Act
        logger.LogInformation("Hello, world!");
        StopApp();

        // Assert
        var rolledLogPath = GetLogPath(TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss"));

        FileSystem.ShouldSatisfyAllConditions(fs => fs
                .File
                .Exists(rolledLogPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(rolledLogPath)
                .ShouldBe(logExistingText),
            fs => fs
                .File
                .Exists(logPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(logPath)
                .ShouldBe("""
                          [2020-01-02 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.RolloverTests] Hello, world!

                          """));
    }

    [Test]
    public void Logger_WhenNewLogIsCreatedAndMaxTotalSizeExceeded_OldestLogIsRemoved()
    {
        // Arrange
        var firstLogTimestamp = TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss");

        for (var i = 0; i < 10; i++)
        {
            var timestamp = TimeProvider
                .GetLocalNow()
                .AddDays(i)
                .ToString("yyMMdd-HHmmss");

            var logPath = GetLogPath(timestamp);

            FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(logPath)!);
            FileSystem.File.WriteAllText(logPath, $"{new string('a', 1024)}\n");
        }

        var logger = CreateBuilderWithLogger<RolloverTests>(c =>
        {
            c.FileSizeLimitBytes = 1L * 1024;
            c.RolloverInterval = FileRolloverInterval.Infinite;
            c.MaxTotalSizeBytes = 10L * 1024;
        });

        TimeProvider.UtcNow = TimeProvider.UtcNow.AddDays(10);

        // Act
        logger.LogInformation("Hello, world!");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs
                .Directory
                .GetFiles(FileSystem.Path.Combine(FileSystem.Directory.GetCurrentDirectory(), "Logs"))
                .Length
                .ShouldBe(10),
            fs => fs
                .Directory
                .GetFiles(FileSystem.Path.Combine(FileSystem.Directory.GetCurrentDirectory(), "Logs"))
                .ShouldNotContain(FileSystem.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    "Logs",
                    $"{firstLogTimestamp}.log")),
            fs => fs
                .File
                .ReadAllText(FileSystem.Path.Combine(FileSystem.Directory.GetCurrentDirectory(),
                    "Logs",
                    $"{AppDomain.CurrentDomain.FriendlyName}.log"))
                .ShouldBe("""
                          [2020-01-11 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.RolloverTests] Hello, world!

                          """));
    }
}
