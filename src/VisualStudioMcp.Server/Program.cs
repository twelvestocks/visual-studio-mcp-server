using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VisualStudioMcp.Core;
using VisualStudioMcp.Debug;
using VisualStudioMcp.Imaging;
using VisualStudioMcp.Server;
using VisualStudioMcp.Xaml;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(ConfigureServices)
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .UseConsoleLifetime()
    .Build();

try
{
    var mcpServer = host.Services.GetRequiredService<VisualStudioMcpServer>();
    await mcpServer.RunAsync(host.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping);
}
catch (Exception ex)
{
    var logger = host.Services.GetRequiredService<ILogger<Program>>();
    logger.LogCritical(ex, "Fatal error starting Visual Studio MCP Server");
    Environment.Exit(1);
}

static void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<VisualStudioMcpServer>();
    services.AddSingleton<IVisualStudioService, VisualStudioService>();
    services.AddSingleton<IXamlDesignerService, XamlDesignerService>();
    services.AddSingleton<IDebugService, DebugService>();
    services.AddSingleton<IImagingService, ImagingService>();
}