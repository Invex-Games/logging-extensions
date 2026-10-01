namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Exercises routing, archive reservations, and retention when filesystem casing differs from separator defaults.
/// </summary>
public sealed class FileSystemCaseTests
{
    /// <summary>
    ///     Case aliases retain enqueue order, while case-sensitive routes retain separate contents.
    /// </summary>
    /// <param name="caseSensitive">Whether different filename casing selects separate files.</param>
    /// <param name="separator">The unrelated directory separator reported by the filesystem.</param>
    [TestCase(false, '/')]
    [TestCase(true, '\\')]
    public void BufferedWriter_UsesFilesystemCasingForBuckets(bool caseSensitive, char separator)
    {
        var storage = new CaseSensitiveFileSystem(caseSensitive, separator);
        var config = CreateConfiguration();

        config.PerGroupLogName = new()
        {
            ["lower"] = "shared",
            ["upper"] = "SHARED",
        };

        using var writer = new BufferedFileLogWriter(storage.FileSystem, new TestTimeProvider(), () => config);

        for (var index = 0; index < 25; index++)
            writer.Log($"Entry {index}\n",
                LogLevel.Information,
                index % 2 == 0
                    ? "lower"
                    : "upper");

        writer.Start();
        writer.Dispose();

        var lowerEntries = Enumerable
            .Range(0, 25)
            .Where(index => !caseSensitive || index % 2 == 0);

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath("app_shared.log"))
            .ShouldBe(string.Concat(lowerEntries.Select(index => $"Entry {index}\n")));

        if (caseSensitive)
            storage
                .FileSystem
                .File
                .ReadAllText(storage.LogPath("app_SHARED.log"))
                .ShouldBe(string.Concat(Enumerable
                    .Range(0, 25)
                    .Where(index => index % 2 != 0)
                    .Select(index => $"Entry {index}\n")));

        storage
            .AllFiles
            .Count()
            .ShouldBe(caseSensitive
                ? 2
                : 1);
    }

    /// <summary>
    ///     Probe failures are retried, cleaned up, and confined to the affected entry without stopping later writes.
    /// </summary>
    [Test]
    public void BufferedWriter_ContinuesAfterRepeatedProbeFailures()
    {
        var storage = new CaseSensitiveFileSystem(false, '/');
        var config = CreateConfiguration();

        config.PerGroupLogName = new()
        {
            ["lower"] = "shared",
            ["upper"] = "SHARED",
        };

        A
            .CallTo(() =>
                storage.FileSystem.File.Exists(A<string>.That.Matches(path =>
                    path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))))
            .Throws<IOException>()
            .NumberOfTimes(6);

        using var writer = new BufferedFileLogWriter(storage.FileSystem, new TestTimeProvider(), () => config);
        writer.Log("First entry\n", LogLevel.Information, "lower");
        writer.Log("Dropped entry\n", LogLevel.Information, "upper");
        writer.Log("Later entry\n", LogLevel.Information, "upper");
        writer.Start();
        writer.Dispose();

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath("app_shared.log"))
            .ShouldBe("First entry\nLater entry\n");

        storage.AllFiles.ShouldBe([storage.LogPath("app_shared.log")]);
    }

    /// <summary>
    ///     Differently cased directories are compared by identity even when the leaf filename has identical spelling.
    /// </summary>
    /// <param name="caseSensitive">Whether directory casing identifies different directories.</param>
    /// <param name="separator">The unrelated filesystem separator.</param>
    [TestCase(false, '/')]
    [TestCase(true, '\\')]
    public void BufferedWriter_UsesDirectoryIdentityForBuckets(bool caseSensitive, char separator)
    {
        var storage = new CaseSensitiveFileSystem(caseSensitive, separator);
        storage.FileSystem.Directory.CreateDirectory(storage.LogPath("app_lower"));
        storage.FileSystem.Directory.CreateDirectory(storage.LogPath("app_LOWER"));
        var config = CreateConfiguration();

        config.PerGroupLogName = new()
        {
            ["lower"] = "lower/entry",
            ["upper"] = "LOWER/entry",
        };

        using var writer = new BufferedFileLogWriter(storage.FileSystem, new TestTimeProvider(), () => config);
        writer.Log("First entry\n", LogLevel.Information, "lower");
        writer.Log("Second entry\n", LogLevel.Information, "upper");
        writer.Log("Third entry\n", LogLevel.Information, "lower");
        writer.Start();
        writer.Dispose();

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath("app_lower/entry.log"))
            .ShouldBe(caseSensitive
                ? "First entry\nThird entry\n"
                : "First entry\nSecond entry\nThird entry\n");

        if (caseSensitive)
            storage
                .FileSystem
                .File
                .ReadAllText(storage.LogPath("app_LOWER/entry.log"))
                .ShouldBe("Second entry\n");

        storage
            .AllFiles
            .Count()
            .ShouldBe(caseSensitive
                ? 2
                : 1);
    }

    /// <summary>
    ///     Rollover reserves a differently cased active destination before its first write only when it aliases the archive.
    /// </summary>
    /// <param name="buffered">Whether the writer batches entries.</param>
    /// <param name="caseSensitive">Whether case distinguishes destinations.</param>
    /// <param name="separator">The unrelated filesystem separator.</param>
    [TestCase(true, false, '/')]
    [TestCase(false, false, '/')]
    [TestCase(true, true, '\\')]
    [TestCase(false, true, '\\')]
    public void Writer_ReservesCaseAliasesBeforeActiveFileExists(bool buffered, bool caseSensitive, char separator)
    {
        var storage = new CaseSensitiveFileSystem(caseSensitive, separator);
        storage.AddFile("app_shared.log", "Previous entry");
        var config = CreateConfiguration();
        config.FileSizeLimitBytes = 1;

        config.PerGroupLogName = new()
        {
            ["writing"] = "shared",
            ["reserved"] = "SHARED_200101-110000",
        };

        Write(storage, config, buffered, "writing");

        var archive = caseSensitive
            ? "app_shared_200101-110000.log"
            : "app_shared_200101-110000_1.log";

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath(archive))
            .ShouldBe("Previous entry");

        storage
            .FileSystem
            .File
            .Exists(storage.LogPath("app_SHARED_200101-110000.log"))
            .ShouldBeFalse();

        storage
            .AllFiles
            .Count()
            .ShouldBe(2);
    }

    /// <summary>
    ///     Retention includes alternate archive casing and extensions only when they address this route's files.
    /// </summary>
    /// <param name="buffered">Whether the writer batches entries.</param>
    /// <param name="caseSensitive">Whether case distinguishes destinations.</param>
    /// <param name="separator">The unrelated filesystem separator.</param>
    [TestCase(true, false, '/')]
    [TestCase(false, false, '/')]
    [TestCase(true, true, '\\')]
    [TestCase(false, true, '\\')]
    public void Writer_RetentionUsesActualFilenameAndExtensionCasing(bool buffered, bool caseSensitive, char separator)
    {
        var storage = new CaseSensitiveFileSystem(caseSensitive, separator);
        const string archive = "APP_191231-110000.LOG";
        storage.AddFile(archive, "Previous archive");
        var config = CreateConfiguration();
        config.MaxTotalSizeBytes = 1;

        Write(storage, config, buffered);

        storage
            .FileSystem
            .File
            .Exists(storage.LogPath(archive))
            .ShouldBe(caseSensitive);

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath("app.log"))
            .ShouldBe("New entry\n");

        storage
            .AllFiles
            .Count()
            .ShouldBe(caseSensitive
                ? 2
                : 1);
    }

    /// <summary>
    ///     A differently cased active route is protected from retention on a case-insensitive filesystem.
    /// </summary>
    /// <param name="buffered">Whether the writer batches entries.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void Writer_RetentionProtectsActiveCaseAliases(bool buffered)
    {
        var storage = new CaseSensitiveFileSystem(false, '/');
        const string active = "APP_SHARED_191231-110000.LOG";
        storage.AddFile(active, "Active route");
        var config = CreateConfiguration();
        config.MaxTotalSizeBytes = 1;

        config.PerGroupLogName = new()
        {
            ["writing"] = "shared",
            ["active"] = "shared_191231-110000",
        };

        Write(storage, config, buffered, "writing");

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath(active))
            .ShouldBe("Active route");

        storage
            .AllFiles
            .Count()
            .ShouldBe(2);
    }

    /// <summary>
    ///     An unwritten route in a missing directory cannot interrupt a healthy route's first write.
    /// </summary>
    /// <param name="buffered">Whether the writer batches entries.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void Writer_IgnoresMissingInactiveRouteDirectory(bool buffered)
    {
        var storage = new CaseSensitiveFileSystem(false, '/');
        var config = CreateConfiguration();

        config.PerGroupLogName = new()
        {
            ["inactive"] = "missing/APP",
            ["other"] = "MISSING/app",
        };

        Write(storage, config, buffered);

        storage
            .FileSystem
            .File
            .ReadAllText(storage.LogPath("app.log"))
            .ShouldBe("New entry\n");

        storage
            .AllFiles
            .Count()
            .ShouldBe(1);
    }

    /// <summary>
    ///     Creates a configuration that isolates filename behavior from time rollover.
    /// </summary>
    /// <returns>The configuration shared by casing scenarios.</returns>
    private static FileLoggerConfiguration CreateConfiguration() =>
        new()
        {
            LogName = "app",
            LogDirectory = "Logs",
            RolloverInterval = FileRolloverInterval.Infinite,
        };

    /// <summary>
    ///     Writes and flushes one entry through either production writer.
    /// </summary>
    /// <param name="storage">The mock filesystem facade.</param>
    /// <param name="config">The configuration for the write.</param>
    /// <param name="buffered">Whether to use the background writer.</param>
    /// <param name="group">The optional route group.</param>
    private static void Write(
        CaseSensitiveFileSystem storage,
        FileLoggerConfiguration config,
        bool buffered,
        string? group = null)
    {
        using IFileLogWriter writer = buffered
            ? new BufferedFileLogWriter(storage.FileSystem, new TestTimeProvider(), () => config)
            : new DirectFileLogWriter(storage.FileSystem, new TestTimeProvider(), () => config);

        writer.Log("New entry\n", LogLevel.Information, group);

        if (writer is BufferedFileLogWriter backgroundWriter)
            backgroundWriter.Start();
    }
}
