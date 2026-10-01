namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class TimestampTests : TestBase
{
    [Test]
    public void Timestamps_Use_Local_Time_By_Default()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.LogInformation("Hello");

        Output.ShouldStartWith("2020-01-01 11:00:00.000 +11:00 Information\n");
    }

    [Test]
    public void UseUtcTimestamp_Writes_Utc_Times()
    {
        var logger = CreateLogger(c => c.UseUtcTimestamp = true);

        logger.LogInformation("Hello");

        Output.ShouldBe("2020-01-01 +00:00 MyApp.Services.OrderService\n00:00:00.000  INF Hello\n\n");
    }

    [Test]
    public void Timestamps_Reflect_The_Current_Time()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.SingleLine);

        logger.LogInformation("First");

        TimeProvider.UtcNow = TimeProvider
            .UtcNow
            .AddMinutes(1)
            .AddMilliseconds(234);

        logger.LogInformation("Second");

        Output.ShouldBe(Lf("""
                           11:00:00.000 INF MyApp.Services.OrderService: First
                           11:01:00.234 INF MyApp.Services.OrderService: Second

                           """));
    }

    [TestCase(FancyConsoleLayout.Standard, "2020-01-01 +11:00 MyApp.Services.OrderService\n[11:00:00]  INF Hello\n\n")]
    [TestCase(FancyConsoleLayout.SingleLine, "[11:00:00] INF MyApp.Services.OrderService: Hello\n")]
    [TestCase(FancyConsoleLayout.Minimal, "INF Hello\n")]
    public void TimestampFormat_Overrides_The_Layout_Format(FancyConsoleLayout layout, string expected)
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = layout;
            c.TimestampFormat = "[HH:mm:ss]";
        });

        logger.LogInformation("Hello");

        Output.ShouldBe(expected);
    }

    [Test]
    public void TimestampFormat_Overrides_The_Detailed_Format()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Detailed;
            c.TimestampFormat = "O";
        });

        logger.LogInformation("Hello");

        Output.ShouldStartWith("2020-01-01T11:00:00.0000000+11:00 Information\n");
    }

    [Test]
    public void Standard_Continuation_Indent_Follows_The_TimestampFormat()
    {
        var logger = CreateLogger(c => c.TimestampFormat = "HH:mm");

        logger.LogInformation("Line 1\nLine 2");

        Output.ShouldContain("11:00  INF Line 1\n           Line 2\n");
    }

    [Test]
    public void Invalid_TimestampFormat_Falls_Back_To_The_Layout_Format()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.SingleLine;
            c.TimestampFormat = "%";
        });

        logger.LogInformation("Hello");

        Output.ShouldBe("11:00:00.000 INF MyApp.Services.OrderService: Hello\n");
    }
}
