using Invex.Extensions.Logging.FancyConsole.Configuration;

namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class ProviderTests : TestBase
{
    [Test]
    public void Entries_Are_Written_To_Standard_Output_By_Default()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal);

        logger.LogCritical("Hello");

        Output.ShouldBe("CRT Hello\n");
        ErrorOutput.ShouldBeEmpty();
    }

    [Test]
    public void LogToStandardErrorThreshold_Routes_Entries_To_Standard_Error()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.LogToStandardErrorThreshold = LogLevel.Warning;
        });

        logger.LogInformation("Info");
        logger.LogWarning("Warning");
        logger.LogError("Error");

        Output.ShouldBe("INF Info\n");
        ErrorOutput.ShouldBe("WRN Warning\nERR Error\n");
    }

    [Test]
    public void Empty_Messages_Are_Skipped()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal);

        logger.LogInformation("");
        logger.Log(LogLevel.Information, default, "ignored", null, static (_, _) => null!);
        logger.LogInformation("Written");

        Output.ShouldBe("INF Written\n");
    }

    [Test]
    public void LogLevel_None_Is_Not_Enabled()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal);

        var provider = App
            .Services
            .GetServices<ILoggerProvider>()
            .OfType<FancyConsoleLoggerProvider>()
            .Single();

        var providerLogger = provider.CreateLogger(Category);

        providerLogger
            .IsEnabled(LogLevel.None)
            .ShouldBeFalse();

        providerLogger
            .IsEnabled(LogLevel.Trace)
            .ShouldBeTrue();

        providerLogger.Log(LogLevel.None, "Hello");
        logger.Log(LogLevel.None, "Hello");

        Output.ShouldBeEmpty();
    }

    [Test]
    public void Loggers_Are_Cached_Per_Category()
    {
        CreateLogger();

        var provider = App
            .Services
            .GetServices<ILoggerProvider>()
            .OfType<FancyConsoleLoggerProvider>()
            .Single();

        provider
            .CreateLogger("A")
            .ShouldBeSameAs(provider.CreateLogger("A"));

        provider
            .CreateLogger("A")
            .ShouldNotBeSameAs(provider.CreateLogger("B"));
    }

    [Test]
    public void Registering_Multiple_Times_Adds_One_Provider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder
            .AddFancyConsole()
            .AddFancyConsole(c => c.Layout = FancyConsoleLayout.Minimal));

        using var provider = services.BuildServiceProvider();

        provider
            .GetServices<ILoggerProvider>()
            .OfType<FancyConsoleLoggerProvider>()
            .Count()
            .ShouldBe(1);
    }

    [Test]
    public void Configuration_Is_Bound_From_The_FancyConsole_Section()
    {
        var logger = CreateLogger(settings: new Dictionary<string, string?>
        {
            ["Logging:FancyConsole:Layout"] = "SingleLine",
            ["Logging:FancyConsole:TimestampFormat"] = "HH:mm",
            ["Logging:FancyConsole:UseShortCategoryName"] = "true",
            ["Logging:FancyConsole:IncludeScopes"] = "true",
            ["Logging:FancyConsole:ExceptionFormat"] = "Summary",
            ["Logging:FancyConsole:LogToStandardErrorThreshold"] = "Error",
            ["Logging:FancyConsole:LevelStyles:Information"] = "bold",
            ["Logging:FancyConsole:UseColors"] = "false",
            ["Logging:FancyConsole:UseUtcTimestamp"] = "true",
            ["Logging:FancyConsole:ExceptionTextStyle"] = "blue",
        });

        var options = App.Services.GetRequiredService<IOptionsMonitor<FancyConsoleLoggerConfiguration>>()
            .CurrentValue;

        options
            .LevelStyles[LogLevel.Information]
            .ShouldBe("bold");

        options.ExceptionTextStyle.ShouldBe("blue");
        options.UseColors.ShouldBeFalse();

        using (logger.BeginScope("Scoped"))
        {
            logger.LogInformation("Hello");
            logger.LogError(new InvalidOperationException("Boom"), "Failed");
        }

        Output.ShouldBe("00:00 INF OrderService => Scoped: Hello\n");
        ErrorOutput.ShouldBe("00:00 ERR OrderService => Scoped: Failed | InvalidOperationException: Boom\n");
    }

    [Test]
    public void Code_Configuration_Overrides_Bound_Configuration()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal,
            settings: new Dictionary<string, string?>
            {
                ["Logging:FancyConsole:Layout"] = "Detailed",
            });

        logger.LogInformation("Hello");

        Output.ShouldBe("INF Hello\n");
    }

    [Test]
    public void Configuration_Changes_Apply_To_Subsequent_Entries()
    {
        var logger = CreateLogger(settings: new Dictionary<string, string?>
        {
            ["Logging:FancyConsole:Layout"] = "Minimal",
        });

        logger.LogInformation("First");

        var configuration = (IConfigurationRoot)App.Services.GetRequiredService<IConfiguration>();
        configuration["Logging:FancyConsole:Layout"] = "SingleLine";
        configuration.Reload();

        logger.LogInformation("Second");

        Output.ShouldBe("INF First\n11:00:00.000 INF MyApp.Services.OrderService: Second\n");
    }

    [Test]
    public void Level_Filtering_Uses_The_FancyConsole_Alias()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal,
            settings: new Dictionary<string, string?>
            {
                ["Logging:FancyConsole:LogLevel:Default"] = "Warning",
            });

        logger.LogInformation("Hidden");
        logger.LogWarning("Shown");

        Output.ShouldBe("WRN Shown\n");
    }

    [Test]
    public void Logging_Never_Throws()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.IncludeScopes = true;
        });

        using (logger.BeginScope(new ThrowingScope()))
            Should.NotThrow(() => logger.LogInformation("Dropped"));

        Should.NotThrow(() => logger.Log<object?>(LogLevel.Information,
            default,
            null,
            null,
            static (_, _) => throw new InvalidOperationException("Formatter failure")));

        logger.LogInformation("Written");

        Output.ShouldBe("INF Written\n");
    }

    [Test]
    public void Concurrent_Entries_Are_Not_Interleaved()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Standard);

        Parallel.For(0, 50, i => logger.LogInformation("Message {Index}\nSecond line {Line}", i, i));

        var lines = Output.Split('\n');

        for (var i = 0; i < 50; i++)
        {
            var messageLine = Array.IndexOf(lines, $"11:00:00.000  INF Message {i}");
            messageLine.ShouldBeGreaterThan(0);

            lines[messageLine - 1]
                .ShouldBe("2020-01-01 +11:00 MyApp.Services.OrderService");

            lines[messageLine + 1]
                .ShouldBe($"                  Second line {i}");
        }
    }

    private sealed class ThrowingScope
    {
        public override string ToString() =>
            throw new InvalidOperationException("Scope failure");
    }
}
