namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Tests for the buffered write pipeline: batch handling and flush-on-dispose behavior of
///     <see cref="Writer.BufferedFileLogWriter" />.
/// </summary>
[TestFixture]
public sealed class BufferingTests : TestBase
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
    public void Buffered_Logger_Does_Not_Drop_Entries_Across_Batch_Boundaries()
    {
        // Arrange
        const int entryCount = 25; // more than two full batches of 10
        var logPath = GetLogPath();
        var logger = CreateBuilderWithLogger<BufferingTests>();

        // Act
        for (var i = 0; i < entryCount; i++)
            logger.LogInformation("Message {Number}", i);

        StopApp();

        // Assert
        var lines = FileSystem
            .File
            .ReadAllLines(logPath);

        lines.Length.ShouldBe(entryCount);

        for (var i = 0; i < entryCount; i++)
            lines[i]
                .ShouldEndWith($"Message {i}");
    }

    [Test]
    public void Buffered_Logger_Flushes_Pending_Entries_On_Dispose()
    {
        // Arrange
        const int entryCount = 50;
        var logPath = GetLogPath();
        var logger = CreateBuilderWithLogger<BufferingTests>();

        // Act
        for (var i = 0; i < entryCount; i++)
            logger.LogInformation("Message {Number}", i);

        // Dispose immediately, without giving the background thread time to settle; disposal must
        // drain everything still queued on the channel before returning.
        StopApp(waitBeforeDispose: false);

        // Assert
        var lines = FileSystem
            .File
            .ReadAllLines(logPath);

        lines.Length.ShouldBe(entryCount);

        for (var i = 0; i < entryCount; i++)
            lines[i]
                .ShouldEndWith($"Message {i}");
    }
}

