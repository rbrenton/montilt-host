// CommandHandler.cs
using MonTilt.Core;
using MonTilt.Driver;
using System;
using System.Collections.Generic;

namespace MonTilt.CLI
{
    public class CommandHandler
    {
        private readonly ConfigManager _configManager;
        private readonly MonitorOrientationDriver _driver;
        private readonly DeviceManager _deviceManager;
        private BackgroundService _backgroundService;
        private LogLevel _logLevel = LogLevel.Info;

        public CommandHandler()
        {
            _configManager = new ConfigManager();
            _driver = new MonitorOrientationDriver();
            _deviceManager = new DeviceManager(_driver);
            
            // Load saved configuration if available
            _configManager.LoadConfiguration(_deviceManager);
        }

        public void Start()
        {
            _deviceManager.StartDeviceDiscovery();
            Console.WriteLine("Device discovery started");
        }

        public void Stop()
        {
            _deviceManager.StopDeviceDiscovery();
            _backgroundService?.Stop();
            Console.WriteLine("Device discovery stopped");
        }

        public void StartService(Action<string> logCallback)
        {
            _backgroundService = new BackgroundService(_driver, logCallback);
            _backgroundService.Start();
        }

        public void StopService()
        {
            _backgroundService?.Stop();
        }

        public void HandleCommand(string command)
        {
            string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string mainCommand = parts[0].ToLower();

            try
            {
                switch (mainCommand)
                {
                    case "help":
                        ShowHelp();
                        break;

                    case "list":
                        if (parts.Length > 1)
                        {
                            string subCommand = parts[1].ToLower();
                            switch (subCommand)
                            {
                                case "monitors":
                                    ListMonitors();
                                    break;
                                case "devices":
                                    ListDevices();
                                    break;
                                case "mappings":
                                    ListMappings();
                                    break;
                                default:
                                    Console.WriteLine($"Unknown list subcommand: {subCommand}");
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Missing list subcommand. Try 'list monitors', 'list devices', or 'list mappings'");
                        }
                        break;

                    case "map":
                        if (parts.Length >= 3)
                        {
                            string mac = parts[1];
                            string monitor = parts[2];
                            MapDeviceToMonitor(mac, monitor);
                        }
                        else
                        {
                            Console.WriteLine("Usage: map <mac> <monitor>");
                        }
                        break;

                    case "unmap":
                        if (parts.Length >= 2)
                        {
                            string mac = parts[1];
                            UnmapDevice(mac);
                        }
                        else
                        {
                            Console.WriteLine("Usage: unmap <mac>");
                        }
                        break;

                    case "scan":
                        ScanForDevices();
                        break;

                    case "save":
                        SaveConfiguration();
                        break;

                    case "log":
                        if (parts.Length >= 2)
                        {
                            SetLogLevel(parts[1]);
                        }
                        else
                        {
                            Console.WriteLine("Usage: log <level> (debug, info, warning, error)");
                        }
                        break;

                    default:
                        Console.WriteLine($"Unknown command: {mainCommand}. Type 'help' for a list of commands.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error executing command: {ex.Message}");
                if (_logLevel == LogLevel.Debug)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }

        private void ShowHelp()
        {
            Console.WriteLine("MonTilt Commands:");
            Console.WriteLine("help                     - Show this help text");
            Console.WriteLine("list monitors            - List available monitors");
            Console.WriteLine("list devices             - List connected devices");
            Console.WriteLine("list mappings            - List device-to-monitor mappings");
            Console.WriteLine("map <mac> <monitor>      - Map a device to a monitor");
            Console.WriteLine("unmap <mac>              - Remove a device mapping");
            Console.WriteLine("scan                     - Scan for new devices");
            Console.WriteLine("save                     - Save current configuration");
            Console.WriteLine("log <level>              - Set log level (debug, info, warning, error)");
            Console.WriteLine("exit                     - Exit the application");
        }

        private void ListMonitors()
        {
            var monitors = _driver.GetMonitors();
            Console.WriteLine($"Found {monitors.Count} monitors:");
            foreach (var monitor in monitors)
            {
                Console.WriteLine($"  {monitor.Id}: {monitor.Name} ({monitor.CurrentOrientation})");
            }
        }

        private void ListDevices()
        {
            var devices = _deviceManager.GetConnectedDevices();
            Console.WriteLine($"Found {devices.Count} devices:");
            foreach (var device in devices)
            {
                Console.WriteLine($"  {device.MacAddress}: {device.DeviceName} ({(device.IsConnected ? "Connected" : "Disconnected")})");
            }
        }

        private void ListMappings()
        {
            var mappings = _deviceManager.GetDeviceMappings();
            if (mappings.Count == 0)
            {
                Console.WriteLine("No device-to-monitor mappings configured.");
                return;
            }

            Console.WriteLine("Device-to-Monitor Mappings:");
            foreach (var mapping in mappings)
            {
                Console.WriteLine($"  Device {mapping.DeviceMac} -> Monitor {mapping.MonitorId}");
            }
        }

        private void MapDeviceToMonitor(string mac, string monitorId)
        {
            _deviceManager.MapDeviceToMonitor(mac, monitorId);
            Console.WriteLine($"Mapped device {mac} to monitor {monitorId}");
        }

        private void UnmapDevice(string mac)
        {
            _deviceManager.UnmapDevice(mac);
            Console.WriteLine($"Unmapped device {mac}");
        }

        private void ScanForDevices()
        {
            Console.WriteLine("Scanning for devices...");
            _deviceManager.ScanForDevices();
            Console.WriteLine("Scan complete");
        }

        private void SaveConfiguration()
        {
            _configManager.SaveConfiguration(_deviceManager);
            Console.WriteLine("Configuration saved");
        }

        private void SetLogLevel(string level)
        {
            switch (level.ToLower())
            {
                case "debug":
                    _logLevel = LogLevel.Debug;
                    break;
                case "info":
                    _logLevel = LogLevel.Info;
                    break;
                case "warning":
                    _logLevel = LogLevel.Warning;
                    break;
                case "error":
                    _logLevel = LogLevel.Error;
                    break;
                default:
                    Console.WriteLine($"Unknown log level: {level}");
                    return;
            }
            Console.WriteLine($"Log level set to {_logLevel}");
        }
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}