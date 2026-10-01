namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Verifies that failures preparing routed files are retried without escaping or stopping the worker.
/// </summary>
public sealed class WriterFailureTests : TestBase
{
    /// <summary>
    ///     Installs the deterministic clock and backing mock file system.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        FileSystem = new();
        TimeProvider = new();
        BufferedFileLoggerProvider.TimeProvider = DirectFileLoggerProvider.TimeProvider = TimeProvider;
    }

    /// <summary>
    ///     Directory creation retries recover transient failures; exhausted retries drop only the affected
    ///     route and subsequent entries can still be persisted.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="failureCount">The number of initial directory creation attempts to fail.</param>
    [TestCase(true, 2)]
    [TestCase(false, 2)]
    [TestCase(true, 6)]
    [TestCase(false, 6)]
    public void DirectoryFailures_AreRetriedAndDoNotStopLaterRoutes(bool buffered, int failureCount)
    {
        var directory = A.Fake<IDirectory>();
        var fileSystem = A.Fake<IFileSystem>();
        var attempts = 0;

        A
            .CallTo(() => fileSystem.Path)
            .Returns(FileSystem.Path);

        A
            .CallTo(() => fileSystem.File)
            .Returns(FileSystem.File);

        A
            .CallTo(() => fileSystem.FileInfo)
            .Returns(FileSystem.FileInfo);

        A
            .CallTo(() => fileSystem.Directory)
            .Returns(directory);

        A
            .CallTo(() => directory.GetCurrentDirectory())
            .Returns(FileSystem.Directory.GetCurrentDirectory());

        A
            .CallTo(() => directory.Exists(A<string>._))
            .ReturnsLazily((string path) => FileSystem.Directory.Exists(path));

        A
            .CallTo(() => directory.GetFiles(A<string>._, A<string>._))
            .ReturnsLazily((string path, string pattern) => FileSystem.Directory.GetFiles(path, pattern));

        A
            .CallTo(() => directory.CreateDirectory(A<string>._))
            .ReturnsLazily((string path) =>
            {
                if (Interlocked.Increment(ref attempts) <= failureCount)
                    throw new IOException("Simulated directory failure");

                return FileSystem.Directory.CreateDirectory(path);
            });

        BufferedFileLoggerProvider.FileSystem = DirectFileLoggerProvider.FileSystem = fileSystem;

        var logger = CreateBuilderWithLogger<WriterFailureTests>(config =>
            {
                config.LogName = "app";
                config.PerGroupLogName["First"] = "first";
                config.PerGroupLogName["Second"] = "second";
                config.RolloverInterval = FileRolloverInterval.Infinite;
            },
            buffered);

        Should.NotThrow(() =>
        {
            using (logger.BeginGroupScope("First"))
                logger.LogInformation("First message");

            using (logger.BeginGroupScope("Second"))
                logger.LogInformation("Second message");

            StopApp(false);
        });

        attempts.ShouldBe(failureCount + 1);

        FileSystem
            .File
            .Exists(GetLogPath(customName: "app_first"))
            .ShouldBe(failureCount < 6);

        FileSystem
            .File
            .ReadAllText(GetLogPath(customName: "app_second"))
            .ShouldContain("Second message");
    }

    /// <summary>
    ///     Invalid destinations belonging to other groups do not prevent healthy files from being created
    ///     or rolled over while their archive destinations are reserved.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="rollover">Whether a preexisting healthy file must be rolled over.</param>
    [Test]
    public void InvalidUnselectedRoutes_DoNotPreventHealthyCreationOrRollover(
        [Values] bool buffered,
        [Values] bool rollover)
    {
        BufferedFileLoggerProvider.FileSystem = DirectFileLoggerProvider.FileSystem = FileSystem;
        var path = GetLogPath(customName: "app_healthy");

        if (rollover)
        {
            FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(path)!);
            FileSystem.File.WriteAllText(path, "old content");
        }

        var logger = CreateBuilderWithLogger<WriterFailureTests>(config =>
            {
                config.LogName = "app";
                config.PerGroupLogName["Healthy"] = "healthy";
                config.PerGroupLogName["Invalid"] = "bad\0name";

                config.FileSizeLimitBytes = rollover
                    ? 1
                    : long.MaxValue;

                config.RolloverInterval = FileRolloverInterval.Infinite;
            },
            buffered);

        using (logger.BeginGroupScope("Healthy"))
            logger.LogInformation("Healthy message");

        StopApp(false);

        FileSystem
            .File
            .ReadAllText(path)
            .ShouldContain("Healthy message");

        if (rollover)
        {
            var timestamp = TimeProvider
                .GetLocalNow()
                .ToString("yyMMdd-HHmmss");

            FileSystem
                .File
                .ReadAllText(GetLogPath(timestamp, "app_healthy"))
                .ShouldBe("old content");
        }
    }
}
