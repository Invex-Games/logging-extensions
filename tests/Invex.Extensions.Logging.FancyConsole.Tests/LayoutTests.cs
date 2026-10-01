using System.Text.RegularExpressions;

namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class LayoutTests : TestBase
{
    [Test]
    public void Standard_Is_The_Default_Layout()
    {
        var logger = CreateLogger();

        logger.LogInformation("Hello");

        Output.ShouldBe(Lf("""
                           2020-01-01 +11:00 MyApp.Services.OrderService
                           11:00:00.000  INF Hello


                           """));
    }

    [Test]
    public void Standard_Indents_Continuation_Lines_To_The_Message_Column()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Standard);

        logger.LogWarning("Line 1\nLine 2\r\nLine 3");

        Output.ShouldBe(Lf("""
                           2020-01-01 +11:00 MyApp.Services.OrderService
                           11:00:00.000  WRN Line 1
                                             Line 2
                                             Line 3


                           """));
    }

    [Test]
    public void SingleLine_Writes_One_Line_Per_Entry()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.SingleLine);

        logger.LogInformation("Hello\nWorld");
        logger.LogDebug("Second");

        Output.ShouldBe(Lf("""
                           11:00:00.000 INF MyApp.Services.OrderService: Hello World
                           11:00:00.000 DBG MyApp.Services.OrderService: Second

                           """));
    }

    [Test]
    public void Minimal_Writes_Only_The_Level_And_Message()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal);

        logger.LogError("Hello\nWorld");
        logger.LogCritical("Second");

        Output.ShouldBe(Lf("""
                           ERR Hello
                               World
                           CRT Second

                           """));
    }

    [Test]
    public void Detailed_Writes_Labeled_Fields()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.LogInformation("Hello\nWorld");

        ReplaceThreadId(Output)
            .ShouldBe(Lf("""
                         2020-01-01 11:00:00.000 +11:00 Information
                           Category:  MyApp.Services.OrderService
                           Thread:    <id>
                           Message:   Hello
                                      World


                         """));
    }

    [Test]
    public void Detailed_Includes_The_Managed_Thread_Id()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.LogInformation("Hello");

        Output.ShouldContain($"  Thread:    {Environment.CurrentManagedThreadId}\n");
    }

    [TestCase(42, null, "42")]
    [TestCase(42, "OrderPlaced", "42 (OrderPlaced)")]
    [TestCase(0, "OrderPlaced", "0 (OrderPlaced)")]
    public void Detailed_Includes_The_Event_When_Set(int id, string? name, string expected)
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.Log(LogLevel.Information, new(id, name), "Hello", null, static (s, _) => s);

        Output.ShouldContain($"  Event:     {expected}\n");
    }

    [Test]
    public void Detailed_Omits_An_Empty_Event()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.LogInformation("Hello");

        Output.ShouldNotContain("Event:");
    }

    [TestCase(LogLevel.Trace, "TRC", "Trace")]
    [TestCase(LogLevel.Debug, "DBG", "Debug")]
    [TestCase(LogLevel.Information, "INF", "Information")]
    [TestCase(LogLevel.Warning, "WRN", "Warning")]
    [TestCase(LogLevel.Error, "ERR", "Error")]
    [TestCase(LogLevel.Critical, "CRT", "Critical")]
    public void Levels_Are_Shown_As_Codes_And_Names(LogLevel level, string code, string name)
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal);
        logger.Log(level, "Hello");
        Output.ShouldBe($"{code} Hello\n");

        TearDownApp();
        SetUpConsoles();

        logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);
        logger.Log(level, "Hello");
        Output.ShouldStartWith($"2020-01-01 11:00:00.000 +11:00 {name}\n");
    }

    [Test]
    public void Markup_In_Entries_Is_Written_Literally()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.SingleLine, "[bold]Category[/]");

        logger.LogInformation("[red]not markup[/] {Value}", "[blue]");

        Output.ShouldBe("11:00:00.000 INF [bold]Category[/]: [red]not markup[/] [blue]\n");
    }

    private static string ReplaceThreadId(string output) =>
        Regex.Replace(output, @"Thread:    \d+", "Thread:    <id>");
}
