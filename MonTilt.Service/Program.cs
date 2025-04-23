using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonTilt.Service;
using System;
using System.Diagnostics;
using System.Security.Principal;

// Check if running as administrator on startup
if (!IsRunningAsAdministrator())
{
    Console.WriteLine("WARNING: MonTilt Service is not running with administrator privileges.");
    Console.WriteLine("Display settings changes may fail. Consider running as administrator.");
    
    // For service installation, we require admin rights
    if (args.Length > 0 && args[0].ToLower() == "--install")
    {
        Console.WriteLine("Administrator privileges are required to install the service.");
        Console.WriteLine("Please run the installer as administrator.");
        return;
    }
}

// Create and configure the host
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService(options =>
    {
        options.ServiceName = "MonTilt";
    })
    .ConfigureServices(services =>
    {
        services.AddHostedService<MonTiltService>();
        services.AddSingleton<MonTilt.Core.ConfigManager>();
        services.AddSingleton<MonTilt.Driver.MonitorOrientationDriver>();
        services.AddSingleton<MonTilt.Core.DeviceManager>();
    })
    .ConfigureLogging((hostContext, logging) =>
    {
        logging.AddEventLog(settings =>
        {
            settings.SourceName = "MonTilt";
        });
    })
    .Build();

// Run the service
await host.RunAsync();

bool IsRunningAsAdministrator()
{
    try
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch
    {
        return false;
    }
}