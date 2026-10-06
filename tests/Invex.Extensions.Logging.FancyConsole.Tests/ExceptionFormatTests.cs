using Invex.Extensions.Logging.FancyConsole.Configuration;

namespace Invex.Extensions.Logging.FancyConsole.Tests;

[TestFixture]
public class ExceptionFormatTests : TestBase
{
    private static readonly Exception Exception = new InvalidOperationException("Boom", new ArgumentException("Inner"));

    [Test]
    public void Full_Is_The_Default_Format()
    {
        var logger = CreateLogger();

        logger.LogError(Exception, "Failed");

        Output.ShouldBe(Lf($"""
                            2020-01-01 +11:00 MyApp.Services.OrderService
                            11:00:00.000  ERR Failed
                            {IndentLines(Exception.ToString(), 18)}


                            """));
    }

    [TestCase(FancyConsoleLayout.SingleLine, "11:00:00.000 ERR MyApp.Services.OrderService: Failed")]
    [TestCase(FancyConsoleLayout.Minimal, "ERR Failed")]
    public void Full_Is_Indented_Below_The_Message_In_Compact_Layouts(FancyConsoleLayout layout, string firstLine)
    {
        var logger = CreateLogger(c => c.Layout = layout);

        logger.LogError(Exception, "Failed");

        Output.ShouldBe($"{firstLine}\n{IndentLines(Exception.ToString(), 4)}\n");
    }

    [Test]
    public void Full_Is_Aligned_With_The_Value_Column_In_Detailed()
    {
        var logger = CreateLogger(c => c.Layout = FancyConsoleLayout.Detailed);

        logger.LogError(Exception, "Failed");

        var lines = IndentLines(Exception.ToString(), 13)
            .Substring(13);

        Output.ShouldEndWith($"  Message:   Failed\n  Exception: {lines}\n\n");
    }

    [Test]
    public void Summary_Is_Written_On_Its_Own_Line_In_Standard()
    {
        var logger = CreateLogger(c => c.ExceptionFormat = FancyConsoleExceptionFormat.Summary);

        logger.LogError(Exception, "Failed");

        Output.ShouldBe(Lf("""
                           2020-01-01 +11:00 MyApp.Services.OrderService
                           11:00:00.000  ERR Failed
                                             InvalidOperationException: Boom ---> ArgumentException: Inner


                           """));
    }

    [TestCase(FancyConsoleLayout.SingleLine, "11:00:00.000 ERR MyApp.Services.OrderService: Failed")]
    [TestCase(FancyConsoleLayout.Minimal, "ERR Failed")]
    public void Summary_Is_Written_Inline_In_Compact_Layouts(FancyConsoleLayout layout, string prefix)
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = layout;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Summary;
        });

        logger.LogError(new InvalidOperationException("Multi\nline"), "Failed");

        Output.ShouldBe($"{prefix} | InvalidOperationException: Multi line\n");
    }

    [Test]
    public void Summary_Is_A_Field_In_Detailed()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Detailed;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Summary;
        });

        logger.LogError(Exception, "Failed");

        Output.ShouldEndWith(Lf("""
                                  Message:   Failed
                                  Exception: InvalidOperationException: Boom ---> ArgumentException: Inner


                                """));
    }

    [TestCase(FancyConsoleLayout.Standard, 18)]
    [TestCase(FancyConsoleLayout.SingleLine, 4)]
    [TestCase(FancyConsoleLayout.Minimal, 4)]
    [TestCase(FancyConsoleLayout.Detailed, 13)]
    public void Pretty_Renders_The_Exception_Indented(FancyConsoleLayout layout, int indent)
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = layout;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
        });

        logger.LogError(ThrownException, "Failed");
        logger.LogInformation("After");

        var lines = Output
            .Split('\n')
            .Select(static l => l.TrimEnd())
            .ToArray();

        var exceptionLine = Array.FindIndex(lines, static l => l.Contains("InvalidOperationException: Boom"));

        exceptionLine.ShouldBeGreaterThan(0);

        lines[exceptionLine]
            .ShouldBe($"{new string(' ', indent)}InvalidOperationException: Boom");

        lines.ShouldContain(l => l.StartsWith(new(' ', indent + 1), StringComparison.Ordinal) &&
                                 l.Trim() == "ArgumentException: Inner");

        lines.ShouldContain(l => l.StartsWith(new string(' ', indent + 2) + "at ", StringComparison.Ordinal));
        Output.ShouldNotContain("System.InvalidOperationException");
        Output.ShouldContain("After");
    }

    [Test]
    public void Pretty_Writes_A_Label_Line_In_Detailed()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Detailed;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
        });

        logger.LogError(ThrownException, "Failed");

        Output.ShouldContain("  Message:   Failed\n  Exception:\n             InvalidOperationException: Boom");
    }

    [Test]
    public void Pretty_Writes_Exceptions_That_Were_Never_Thrown()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
        });

        logger.LogError(Exception, "Failed");

        Output.ShouldStartWith("ERR Failed\n    ");
        Output.ShouldContain("InvalidOperationException: Boom");
        Output.ShouldContain("ArgumentException: Inner");
    }

    [Test]
    public void Pretty_Shortens_Thrown_Exception_Stack_Traces()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Minimal;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Pretty;
        });

        try
        {
            throw new InvalidOperationException("Thrown");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed");
        }

        Output.ShouldStartWith("ERR Failed\n    InvalidOperationException: Thrown");
        Output.ShouldContain(nameof(Pretty_Shortens_Thrown_Exception_Stack_Traces));
        Output.ShouldNotContain(typeof(ExceptionFormatTests).FullName!);
    }

    [TestCase(FancyConsoleLayout.Standard,
        "2020-01-01 +11:00 MyApp.Services.OrderService\n11:00:00.000  ERR Failed\n\n")]
    [TestCase(FancyConsoleLayout.SingleLine, "11:00:00.000 ERR MyApp.Services.OrderService: Failed\n")]
    [TestCase(FancyConsoleLayout.Minimal, "ERR Failed\n")]
    public void Hidden_Omits_The_Exception(FancyConsoleLayout layout, string expected)
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = layout;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Hidden;
        });

        logger.LogError(Exception, "Failed");

        Output.ShouldBe(expected);
    }

    [Test]
    public void Hidden_Omits_The_Exception_Field_In_Detailed()
    {
        var logger = CreateLogger(c =>
        {
            c.Layout = FancyConsoleLayout.Detailed;
            c.ExceptionFormat = FancyConsoleExceptionFormat.Hidden;
        });

        logger.LogError(Exception, "Failed");

        Output.ShouldEndWith("  Message:   Failed\n\n");
        Output.ShouldNotContain("Exception");
    }

    [Test]
    public void Messages_Without_Exceptions_Are_Unaffected_By_The_Format()
    {
        foreach (var format in Enum
                     .GetValues(typeof(FancyConsoleExceptionFormat))
                     .Cast<FancyConsoleExceptionFormat>())
        {
            SetUpConsoles();
            var logger = CreateLogger(c => c.ExceptionFormat = format);

            logger.LogInformation("Hello");

            Output.ShouldBe("2020-01-01 +11:00 MyApp.Services.OrderService\n11:00:00.000  INF Hello\n\n");
            TearDownApp();
        }
    }

    private static string IndentLines(string text, int indent)
    {
        var padding = new string(' ', indent);

        return string.Join("\n",
            text
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => padding + line));
    }
}
