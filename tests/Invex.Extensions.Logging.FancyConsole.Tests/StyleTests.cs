namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class StyleTests : TestBase
{
    private const string Escape = "\u001b[";

    [Test]
    public void Level_Text_Uses_The_Default_Level_Style()
    {
        UseAnsiConsole();
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Minimal);

        logger.LogInformation("Hello");

        Output.ShouldBe($"{Render("[skyblue1]INF[/]")} Hello\n");
    }

    [Test]
    public void LevelStyles_Override_The_Default_Style()
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.LevelStyles[LogLevel.Information] = "bold black on yellow";
        });

        logger.LogInformation("Hello");
        logger.LogWarning("Other");

        Output.ShouldBe($"{Render("[bold black on yellow]INF[/]")} Hello\n{Render("[gold1]WRN[/]")} Other\n");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Blank_LevelStyles_Disable_Styling_For_The_Level(string? style)
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.LevelStyles[LogLevel.Information] = style;
        });

        logger.LogInformation("Hello");

        Output.ShouldBe("INF Hello\n");
    }

    [Test]
    public void Invalid_LevelStyles_Fall_Back_To_The_Default_Style()
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.LevelStyles[LogLevel.Information] = "not-a-color";
        });

        logger.LogInformation("Hello");

        Output.ShouldBe($"{Render("[skyblue1]INF[/]")} Hello\n");
    }

    [Test]
    public void Exception_Text_Uses_The_ExceptionTextStyle()
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Summary;
            c.ExceptionTextStyle = "italic";
        });

        logger.LogError(new InvalidOperationException("Boom"), "Failed");

        Output.ShouldBe(
            $"{Render("[darkorange]ERR[/]")} Failed | {Render("[italic]InvalidOperationException: Boom[/]")}\n");
    }

    [Test]
    public void Exception_Text_Uses_Red1_By_Default()
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Summary;
        });

        logger.LogError(new InvalidOperationException("Boom"), "Failed");

        Output.ShouldContain(Render("[red1]InvalidOperationException: Boom[/]"));
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void Blank_ExceptionTextStyle_Disables_Exception_Styling(string? style)
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Summary;
            c.ExceptionTextStyle = style;
        });

        logger.LogError(new InvalidOperationException("Boom"), "Failed");

        Output.ShouldEndWith(" Failed | InvalidOperationException: Boom\n");
    }

    [Test]
    public void Invalid_ExceptionTextStyle_Falls_Back_To_The_Default()
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Summary;
            c.ExceptionTextStyle = "[invalid";
        });

        logger.LogError(new InvalidOperationException("Boom"), "Failed");

        Output.ShouldContain(Render("[red1]InvalidOperationException: Boom[/]"));
    }

    [TestCase(FancyConsoleLayout.Standard, FancyConsoleExceptionFormat.Full)]
    [TestCase(FancyConsoleLayout.SingleLine, FancyConsoleExceptionFormat.Summary)]
    [TestCase(FancyConsoleLayout.Minimal, FancyConsoleExceptionFormat.Pretty)]
    [TestCase(FancyConsoleLayout.Detailed, FancyConsoleExceptionFormat.Pretty)]
    public void UseColors_False_Writes_No_Styling(FancyConsoleLayout layout, FancyConsoleExceptionFormat format)
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = layout;
            c.ExceptionFormat = format;
            c.UseColors = false;
            c.LevelStyles[LogLevel.Error] = "bold";
        });

        logger.LogError(ThrownException, "Failed");

        Output.ShouldContain("Boom");
        Output.ShouldNotContain(Escape);
    }

    [Test]
    public void Pretty_Exceptions_Are_Styled_When_Colors_Are_Enabled()
    {
        UseAnsiConsole();

        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
            c.LevelStyles[LogLevel.Error] = null;
        });

        logger.LogError(ThrownException, "Failed");

        Output.ShouldStartWith("ERR Failed\n");
        Output.ShouldContain(Escape);
    }

    [Test]
    public void Standard_Styles_Both_Header_Lines()
    {
        UseAnsiConsole();
        var logger = CreateLogger();

        logger.LogWarning("Hello");

        Output.ShouldBe(
            $"{Render("[gold1]2020-01-01 +11:00 MyApp.Services.OrderService[/]")}\n{Render("[gold1]11:00:00.000  WRN[/]")} Hello\n\n");
    }

    private static string Render(string markup)
    {
        using var console = new TestConsole()
            .Width(200)
            .Colors(ColorSystem.TrueColor)
            .EmitAnsiSequences();

        console.Write(new Markup(markup));

        return console.Output;
    }
}
