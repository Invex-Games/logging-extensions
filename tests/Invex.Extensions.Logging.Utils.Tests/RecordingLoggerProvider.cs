namespace Invex.Extensions.Logging.Utils.Tests;

/// <summary>
///     Captures category-specific loggers and provider disposal for factory integration tests.
/// </summary>
internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    /// <summary>
    ///     Gets the loggers requested by the factory, keyed by category.
    /// </summary>
    public Dictionary<string, RecordingLogger> Loggers { get; } = [];

    /// <summary>
    ///     Gets the number of times the provider has been disposed.
    /// </summary>
    public int DisposeCount { get; private set; }

    /// <summary>
    ///     Creates or returns a recording logger for a category.
    /// </summary>
    /// <param name="categoryName">The logger's category.</param>
    /// <returns>The recording logger.</returns>
    public ILogger CreateLogger(string categoryName)
    {
        if (!Loggers.TryGetValue(categoryName, out var logger))
        {
            logger = new();
            Loggers.Add(categoryName, logger);
        }

        return logger;
    }

    /// <summary>
    ///     Records provider disposal.
    /// </summary>
    public void Dispose() =>
        DisposeCount++;
}
