using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WebFormsToBlazor.Analysis;
using WebFormsToBlazor.Planning;
using WebFormsToBlazor.Conversion;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(o =>
{
    o.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services
    .AddSingleton<SolutionAnalyzer>()
    .AddSingleton<PlanService>()
    .AddSingleton<ConverterEngine>()
    .AddSingleton<PlanExecutor>();

var host = builder.Build();
await host.RunAsync();