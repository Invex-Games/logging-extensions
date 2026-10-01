namespace Invex.Extensions.Logging.FancyConsole.Tests;

public abstract class TestBase
{
    protected const string Category = "MyApp.Services.OrderService";

    private IHost? _app;

    protected TestConsole Console { get; private set; } = null!;

    protected TestConsole ErrorConsole { get; private set; } = null!;

    protected TestTimeProvider TimeProvider { get; private set; } = null!;

    protected IHost App => _app ?? throw new InvalidOperationException("The app has not been created.");

    /// <summary>
    ///     An <see cref="InvalidOperationException" /> ("Boom") wrapping an <see cref="ArgumentException" /> ("Inner"),
    ///     both of which were thrown and therefore have stack traces.
    /// </summary>
    protected static Exception ThrownException { get; } = CreateThrownException();

    protected string Output => Normalize(Console.Output);

    protected string ErrorOutput => Normalize(ErrorConsole.Output);

    [SetUp]
    public void SetUpConsoles()
    {
        Console = new TestConsole().Width(200);
        ErrorConsole = new TestConsole().Width(200);
        TimeProvider = new();

        FancyConsoleLoggerProvider.Console = Console;
        FancyConsoleLoggerProvider.ErrorConsole = ErrorConsole;
        FancyConsoleLoggerProvider.TimeProvider = TimeProvider;
    }

    [TearDown]
    public void TearDownApp()
    {
        _app?.Dispose();
        _app = null;
        Console.Dispose();
        ErrorConsole.Dispose();
    }

    protected void UseAnsiConsole()
    {
        Console.Dispose();

        Console = new TestConsole()
            .Width(200)
            .Colors(ColorSystem.TrueColor)
            .EmitAnsiSequences();

        FancyConsoleLoggerProvider.Console = Console;
    }

    protected ILogger CreateLogger(
        Action<FancyConsoleLoggerConfiguration>? configure = null,
        string category = Category,
        IDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            DisableDefaults = true,
        });

        if (settings is not null)
            builder.Configuration.AddInMemoryCollection(settings);

        builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        if (configure is not null)
            builder.Logging.AddFancyConsole(configure);
        else
            builder.Logging.AddFancyConsole();

        _app = builder.Build();

        return _app
            .Services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(category);
    }

    protected static string Normalize(string output) =>
        output.Replace("\r\n", "\n");

    /// <summary>
    ///     Normalizes the line endings of an expected value, which may contain CRLF line endings when it is a raw
    ///     string literal in a source file checked out with Windows line endings.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <returns>The expected value with LF line endings.</returns>
    protected static string Lf(string expected) =>
        Normalize(expected);

    private static Exception CreateThrownException()
    {
        try
        {
            try
            {
                throw new ArgumentException("Inner");
            }
            catch (ArgumentException inner)
            {
                throw new InvalidOperationException("Boom", inner);
            }
        }
        catch (InvalidOperationException ex)
        {
            return ex;
        }
    }
}
