namespace Invex.Extensions.Logging.File.Tests;

public abstract class TestBase
{
    private IDisposable? _disposableApp;
    protected MockFileSystem FileSystem = null!;
    protected TestTimeProvider TimeProvider = null!;

    protected string GetLogPath(string? timestamp = null, string? customName = null) =>
        FileSystem.Path.Combine(FileSystem.Directory.GetCurrentDirectory(),
            "Logs",
            timestamp is not null
                ? customName is not null
                    ? $"{customName}_{timestamp}.log"
                    : $"{AppDomain.CurrentDomain.FriendlyName}_{timestamp}.log"
                : customName is not null
                    ? $"{customName}.log"
                    : $"{AppDomain.CurrentDomain.FriendlyName}.log");

    protected ILogger CreateBuilderWithLogger<T>(
        Action<FileLoggerConfiguration>? configure = null,
        bool buffered = true)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        if (configure is not null)
            builder.Logging.AddFile(configure, buffered);
        else
            builder.Logging.AddFile(buffered);

        var app = builder.Build();

        _disposableApp = app;

        return app.Services.GetRequiredService<ILogger<T>>();
    }

    protected void StopApp(bool waitBeforeDispose = true)
    {
        if (waitBeforeDispose)
            Thread.Sleep(100);

        _disposableApp?.Dispose();
    }
}
