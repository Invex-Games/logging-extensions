namespace Invex.Extensions.Logging.File.Tests;

[TestFixture]
public sealed class BasicTests : TestBase
{
    [SetUp]
    public void SetUp()
    {
        FileSystem = new();
        DirectFileLoggerProvider.FileSystem = FileSystem;
        BufferedFileLoggerProvider.FileSystem = FileSystem;

        TimeProvider = new();
        DirectFileLoggerProvider.TimeProvider = TimeProvider;
        BufferedFileLoggerProvider.TimeProvider = TimeProvider;
    }

    [Test]
    public void Logger_Logs_Log()
    {
        // Arrange
        var logPath = GetLogPath();
        var logger = CreateBuilderWithLogger<BasicTests>();

        // Act
        logger.LogInformation("Hello, world!");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs.File.Exists(logPath),
            fs => fs
                .File
                .ReadAllText(logPath)
                .ShouldBe("""
                          [2020-01-01 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.BasicTests] Hello, world!

                          """));
    }

    [Test]
    public void Logger_Respects_Rooted_Config_Path()
    {
        // Arrange
        const string logsDirectory = @"C:\Logs";
        var logPath = FileSystem.Path.Combine(logsDirectory, $"{AppDomain.CurrentDomain.FriendlyName}.log");

        var logger = CreateBuilderWithLogger<BasicTests>(c => c.LogDirectory = logsDirectory);

        // Act
        logger.LogInformation("Hello, world!");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs
                .File
                .Exists(logPath)
                .ShouldBeTrue(),
            fs => fs
                .File
                .ReadAllText(logPath)
                .ShouldBe("""
                          [2020-01-01 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.BasicTests] Hello, world!

                          """));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Logger_Logs_To_Level_Defined_File(bool buffered)
    {
        // Arrange
        var dbgLogPath = GetLogPath(null, $"{AppDomain.CurrentDomain.FriendlyName}_DBG");
        var errLogPath = GetLogPath(null, $"{AppDomain.CurrentDomain.FriendlyName}_ERR");

        var logger = CreateBuilderWithLogger<BasicTests>(config =>
            {
                config.PerLevelLogName = new()
                {
                    { LogLevel.Trace, $"{AppDomain.CurrentDomain.FriendlyName}_DBG" },
                    { LogLevel.Debug, $"{AppDomain.CurrentDomain.FriendlyName}_DBG" },
                    { LogLevel.Information, $"{AppDomain.CurrentDomain.FriendlyName}_DBG" },
                    { LogLevel.Warning, $"{AppDomain.CurrentDomain.FriendlyName}_DBG" },
                    { LogLevel.Error, $"{AppDomain.CurrentDomain.FriendlyName}_ERR" },
                    { LogLevel.Critical, $"{AppDomain.CurrentDomain.FriendlyName}_ERR" },
                };
            },
            buffered);

        // Act
        logger.LogTrace("Hello, world!");
        logger.LogDebug("Hello, world!");
        logger.LogInformation("Hello, world!");
        logger.LogWarning("This is an error message.");
        logger.LogError("This is an error message.");
        logger.LogCritical("This is an error message.");
        StopApp();

        // Assert
        FileSystem.ShouldSatisfyAllConditions(fs => fs.File.Exists(dbgLogPath),
            fs => fs
                .File
                .ReadAllText(dbgLogPath)
                .ShouldBe("""
                          [2020-01-01 11:00:00.000 +11:00 TRC Invex.Extensions.Logging.File.Tests.BasicTests] Hello, world!
                          [2020-01-01 11:00:00.000 +11:00 DBG Invex.Extensions.Logging.File.Tests.BasicTests] Hello, world!
                          [2020-01-01 11:00:00.000 +11:00 INF Invex.Extensions.Logging.File.Tests.BasicTests] Hello, world!
                          [2020-01-01 11:00:00.000 +11:00 WRN Invex.Extensions.Logging.File.Tests.BasicTests] This is an error message.

                          """),
            fs => fs.File.Exists(errLogPath),
            fs => fs
                .File
                .ReadAllText(errLogPath)
                .ShouldBe("""
                          [2020-01-01 11:00:00.000 +11:00 ERR Invex.Extensions.Logging.File.Tests.BasicTests] This is an error message.
                          [2020-01-01 11:00:00.000 +11:00 CRT Invex.Extensions.Logging.File.Tests.BasicTests] This is an error message.

                          """));
    }
}
