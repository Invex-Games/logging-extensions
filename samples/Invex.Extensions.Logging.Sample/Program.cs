using Invex.Extensions.Logging.FancyConsole;
using Invex.Extensions.Logging.File;
using Invex.Extensions.Logging.Sample;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

builder
    .Logging
    .ClearProviders()
    .AddFancyConsole()
    .AddFile();

var host = builder.Build();
host.Run();
