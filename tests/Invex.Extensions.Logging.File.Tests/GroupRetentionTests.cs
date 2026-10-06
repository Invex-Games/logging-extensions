using Invex.Extensions.Logging.File.Configuration;
using Invex.Extensions.Logging.File.Provider;

namespace Invex.Extensions.Logging.File.Tests;

/// <summary>
///     Verifies that group routes roll independently and never consume another route's files.
/// </summary>
public sealed class GroupRetentionTests : TestBase
{
    /// <summary>
    ///     Installs the same deterministic file system and clock for both writer modes.
    /// </summary>
    [SetUp]
    public void SetUp()
    {
        FileSystem = new();
        BufferedFileLoggerProvider.FileSystem = DirectFileLoggerProvider.FileSystem = FileSystem;
        TimeProvider = new();
        BufferedFileLoggerProvider.TimeProvider = DirectFileLoggerProvider.TimeProvider = TimeProvider;
    }

    /// <summary>
    ///     Sibling routes and malformed archive names do not contribute to a group's size cap.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void Purge_IgnoresSiblingRoutesAndMalformedArchives(bool buffered)
    {
        var ownArchive = SeedFile("app_orders_191230-110000", 40, 1);

        var excludedFiles = new[]
        {
            SeedFile("app_orders_error", 1000, 10),
            SeedFile("app_orders_error_191229-110000", 1000, 10),
            SeedFile("app_orders_191229-110000_bad", 1000, 10),
            SeedFile("app_orders_191229-110000_0", 1000, 10),
            SeedFile("app_orders_misc", 1000, 10),
        };

        var logger = CreateLogger(buffered);

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("New order");

        StopApp(false);

        FileSystem
            .File
            .ReadAllText(ownArchive)
            .Length
            .ShouldBe(40);

        foreach (var path in excludedFiles)
            FileSystem
                .File
                .ReadAllText(path)
                .Length
                .ShouldBe(1000);

        FileSystem
            .File
            .ReadAllText(GetLogPath(customName: "app_orders"))
            .ShouldContain("New order");
    }

    /// <summary>
    ///     Collision archives count toward retention, but an older sibling file is not eligible.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="logName">The configured base name, including a relative path alias.</param>
    [TestCase(true, "app")]
    [TestCase(false, "app")]
    [TestCase(true, "./app")]
    [TestCase(false, "./app")]
    public void Purge_DeletesOnlyOldestMatchingArchiveIncludingCollisionSuffix(bool buffered, string logName)
    {
        var recentArchive = SeedFile("app_orders_191230-110000", 60, 1);
        var oldestArchive = SeedFile("app_orders_191230-110000_1", 60, 2);
        var siblingActive = SeedFile("app_orders_error", 1000, 10);
        var siblingArchive = SeedFile("app_orders_error_191229-110000", 1000, 10);
        var logger = CreateLogger(buffered, config => config.LogName = logName);

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("New order");

        StopApp(false);

        FileSystem
            .File
            .Exists(oldestArchive)
            .ShouldBeFalse();

        FileSystem
            .File
            .ReadAllText(recentArchive)
            .Length
            .ShouldBe(60);

        FileSystem
            .File
            .ReadAllText(siblingActive)
            .Length
            .ShouldBe(1000);

        FileSystem
            .File
            .ReadAllText(siblingArchive)
            .Length
            .ShouldBe(1000);
    }

    /// <summary>
    ///     A combined group and level route has its own retention limit.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    [TestCase(true)]
    [TestCase(false)]
    public void Purge_CombinedRouteCountsOnlyItsOwnArchives(bool buffered)
    {
        var recentArchive = SeedFile("app_orders_error_191230-110000", 60, 1);
        var oldestArchive = SeedFile("app_orders_error_191229-110000", 60, 2);
        var groupArchive = SeedFile("app_orders_191228-110000", 1000, 10);
        var otherGroupArchive = SeedFile("app_billing_error_191228-110000", 1000, 10);
        var logger = CreateLogger(buffered);

        using (logger.BeginGroupScope("Orders"))
            logger.LogError("Order failed");

        StopApp(false);

        FileSystem
            .File
            .Exists(oldestArchive)
            .ShouldBeFalse();

        FileSystem
            .File
            .ReadAllText(recentArchive)
            .Length
            .ShouldBe(60);

        FileSystem
            .File
            .ReadAllText(groupArchive)
            .Length
            .ShouldBe(1000);

        FileSystem
            .File
            .ReadAllText(otherGroupArchive)
            .Length
            .ShouldBe(1000);
    }

    /// <summary>
    ///     A timestamp-shaped configured active name must not be mistaken for another route's archive.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="routeKind">The suffix setting that reserves the timestamp-shaped active file.</param>
    [Test]
    public void Purge_ProtectsConfiguredTimestampShapedActiveRoutes(
        [Values] bool buffered,
        [Values("Level", "Group", "GroupLevel", "RelativeBase")] string routeKind)
    {
        var timestamp = TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss");

        var reservedName = routeKind == "GroupLevel"
            ? $"app_{timestamp}_1"
            : $"app_{timestamp}";

        var reservedActive = SeedFile(reservedName, 1000, 10);
        var oldestArchive = SeedFile("app_191229-110000", 60, 2);
        var recentArchive = SeedFile("app_191230-110000", 60, 1);

        var logger = CreateLogger(buffered,
            config =>
            {
                config.PerGroupLogName["Orders"] = null;

                switch (routeKind)
                {
                    case "Level":
                        config.PerLevelLogName[LogLevel.Critical] = timestamp;

                        break;
                    case "Group":
                        config.PerGroupLogName["Orders"] = string.Empty;
                        config.PerGroupLogName["Reserved"] = timestamp;

                        break;
                    case "GroupLevel":
                        config.PerGroupLogName["Reserved"] = timestamp;
                        config.PerLevelLogName[LogLevel.Critical] = "1";

                        break;
                    case "RelativeBase":
                        config.LogName = "./app";
                        config.PerGroupLogName["Reserved"] = timestamp;

                        break;
                }
            });

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("New order");

        StopApp(false);

        FileSystem
            .File
            .ReadAllText(reservedActive)
            .Length
            .ShouldBe(1000);

        FileSystem
            .File
            .Exists(oldestArchive)
            .ShouldBeFalse();

        FileSystem
            .File
            .ReadAllText(recentArchive)
            .Length
            .ShouldBe(60);
    }

    /// <summary>
    ///     Size and elapsed-time rollover both skip configured active names before those files exist.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="timeBased">Whether to trigger time rollover instead of size rollover.</param>
    /// <param name="relativeAlias">Whether the configured base name uses a relative path alias.</param>
    [Test]
    public void Rollover_SkipsConfiguredNamesBeforeTheyExist(
        [Values] bool buffered,
        [Values] bool timeBased,
        [Values] bool relativeAlias)
    {
        var timestamp = TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss");

        var active = SeedFile("app", 1000, 1);

        var logger = CreateLogger(buffered,
            config =>
            {
                config.LogName = relativeAlias
                    ? "./app"
                    : "app";

                config.PerGroupLogName["Orders"] = null;

                config.FileSizeLimitBytes = timeBased
                    ? long.MaxValue
                    : 1;

                config.RolloverInterval = timeBased
                    ? FileRolloverInterval.Day
                    : FileRolloverInterval.Infinite;

                config.MaxTotalSizeBytes = long.MaxValue;

                config.PerLevelLogName[LogLevel.Error] = timestamp;
                config.PerGroupLogName["ReservedCollision"] = $"{timestamp}_1";
            });

        using (logger.BeginGroupScope("Orders"))
            logger.LogInformation("New order");

        StopApp(false);

        FileSystem
            .File
            .Exists(GetLogPath(customName: $"app_{timestamp}"))
            .ShouldBeFalse();

        FileSystem
            .File
            .Exists(GetLogPath(customName: $"app_{timestamp}_1"))
            .ShouldBeFalse();

        FileSystem
            .File
            .ReadAllText(GetLogPath(customName: $"app_{timestamp}_2"))
            .Length
            .ShouldBe(1000);

        FileSystem
            .File
            .ReadAllText(active)
            .ShouldContain("New order");
    }

    /// <summary>
    ///     Size and time rollover preserve the full group and level basename and existing collision rules.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="timeBased">Whether to trigger time rollover instead of size rollover.</param>
    [Test]
    public void Rollover_UsesFullGroupAndLevelName([Values] bool buffered, [Values] bool timeBased)
    {
        var timestamp = TimeProvider
            .GetLocalNow()
            .ToString("yyMMdd-HHmmss");

        var active = SeedFile("app_orders_error", 1000, 1);
        var previousArchive = SeedFile($"app_orders_error_{timestamp}", 20, 1);
        var sibling = SeedFile("app_orders", 30, 1);

        var logger = CreateLogger(buffered,
            config =>
            {
                config.FileSizeLimitBytes = timeBased
                    ? long.MaxValue
                    : 1;

                config.RolloverInterval = timeBased
                    ? FileRolloverInterval.Day
                    : FileRolloverInterval.Infinite;

                config.MaxTotalSizeBytes = long.MaxValue;
            });

        using (logger.BeginGroupScope("Orders"))
            logger.LogError("Order failed");

        StopApp(false);

        FileSystem
            .File
            .ReadAllText(GetLogPath(customName: $"app_orders_error_{timestamp}_1"))
            .Length
            .ShouldBe(1000);

        FileSystem
            .File
            .ReadAllText(previousArchive)
            .Length
            .ShouldBe(20);

        FileSystem
            .File
            .ReadAllText(sibling)
            .Length
            .ShouldBe(30);

        FileSystem
            .File
            .ReadAllText(active)
            .ShouldContain("Order failed");
    }

    /// <summary>
    ///     Creates a logger with group and level routes and a small archive-size cap.
    /// </summary>
    /// <param name="buffered">Whether to use the buffered writer.</param>
    /// <param name="configure">Additional configuration for the scenario.</param>
    /// <returns>The configured logger.</returns>
    private ILogger CreateLogger(bool buffered, Action<FileLoggerConfiguration>? configure = null) =>
        CreateBuilderWithLogger<GroupRetentionTests>(config =>
            {
                config.LogName = "app";
                config.PerGroupLogName["Orders"] = "orders";
                config.PerGroupLogName["Billing"] = "billing";
                config.PerLevelLogName[LogLevel.Error] = "error";
                config.RolloverInterval = FileRolloverInterval.Infinite;
                config.MaxTotalSizeBytes = 100;
                configure?.Invoke(config);
            },
            buffered);

    /// <summary>
    ///     Seeds a deterministic file and creation time in the mock file system.
    /// </summary>
    /// <param name="name">The base filename, including any archive suffix but excluding the extension.</param>
    /// <param name="length">The ASCII content length in bytes.</param>
    /// <param name="daysOld">The number of days before the test clock to use as creation time.</param>
    /// <returns>The full path of the seeded file.</returns>
    private string SeedFile(string name, int length, int daysOld)
    {
        var path = GetLogPath(customName: name);
        FileSystem.Directory.CreateDirectory(FileSystem.Path.GetDirectoryName(path)!);
        FileSystem.File.WriteAllText(path, new('x', length));

        FileSystem.File.SetCreationTimeUtc(path,
            TimeProvider.UtcNow.AddDays(-daysOld)
                .DateTime);

        return path;
    }
}
