using Invex.Extensions.Logging.File;

namespace Invex.Extensions.Logging.Sample;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var ex = new InvalidOperationException("This is a sample exception for logging purposes.");

            logger.LogTrace("This is a trace log message with a value of {Value}.", 42);
            logger.LogTrace("This is a trace log message\nwith a new line.");
            logger.LogTrace(ex, "This is a trace log message with an exception.");
            logger.LogDebug("This is a debug log message.");
            logger.LogDebug(ex, "This is a debug log message with an exception.");
            logger.LogInformation("This is an information log message.");
            logger.LogInformation(ex, "This is an information log message with an exception.");
            logger.LogWarning("This is a warning log message.");
            logger.LogWarning(ex, "This is a warning log message with an exception.");
            logger.LogError("This is an error log message.");
            logger.LogError(ex, "This is an error log message with an exception.");
            logger.LogCritical("This is a critical log message.");
            logger.LogCritical(ex, "This is a critical log message with an exception.");

            using (logger.BeginGroupScope("MyGroup"))
                logger.LogInformation("This is an information log message inside a group scope.");

            await Task.Delay(1000, stoppingToken);
        }
    }
}
