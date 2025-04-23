using System;
using System.Collections.Generic;
using System.Linq;
using MonTilt.Core;
using MonTilt.Driver;

namespace MonTilt.CLI
{
    /// <summary>
    /// Handles command line input and operations for the MonTilt application
    /// </summary>
    public class CommandHandler
    {
        private DeviceManager _deviceManager;
        private ConfigManager _configManager;
        private MonitorOrientationDriver _monitorDriver;

        // Log level constants
        private enum LogLevel
        {
            Debug,
            Info,
            Warning,
            Error
        }

        private LogLevel _currentLogLevel = LogLevel.Info;

        /// <summary>
        /// Initializes a new instance of the CommandHandler
        /// </summary>
        public CommandHandler()
        {
            _configManager = new ConfigManager();
            _monitorDriver = new MonitorOrientationDriver();
            _deviceManager = new DeviceManager(_configManager, _monitorDriver);
            
            // Set up device manager event handlers
            _deviceManager.DeviceDiscovered += (sender, mac) => {
                LogInfo($"Device discovered: {mac}");
            };
            
            _deviceManager.DeviceConnected += (sender, device) => {
                LogInfo($"Device connected: {device.MacAddress} on {device.ComPort}");
                
                // Check if device is mapped
                if (_configManager.DeviceMonitorMap.TryGetValue(device.MacAddress, out int monitorIndex))
                {
                    LogInfo($"Device {device.MacAddress} is mapped to monitor {monitorIndex + 1}");
                }
                else
                {
                    LogInfo($"Device {device.MacAddress} is not mapped to any monitor");
                    LogInfo($"To map this device, use: map {device.MacAddress} [monitor number]");
                }
            };
            
            _deviceManager.DeviceDisconnected += (sender, mac) => {
                LogInfo($"Device disconnected: {mac}");
            };
            
            _deviceManager.OrientationChanged += (sender, args) => {
                var orientationName = args.Orientation switch
                {
                    0 => "Landscape (0°)",
                    1 => "Portrait (90°)",
                    2 => "Landscape flipped (180°)",
                    3 => "Portrait flipped (270°)",
                    _ => $"Unknown ({args.Orientation}°)"
                };
                
                LogDebug($"Orientation changed: {args.DeviceMac} -> {orientationName}");
                LogInfo($"Monitor {args.MonitorIndex + 1} changed to {orientationName}");
            };
        }

        /// <summary>
        /// Starts the command handler, initializing device discovery
        /// </summary>
        public void Start()
        {
            // Load configuration
            _configManager.LoadConfiguration();
            
            // Initialize monitor driver
            _monitorDriver.Initialize();
            
            // Start device discovery
            _deviceManager.StartDeviceDiscovery();
            
            LogInfo("MonTilt started successfully");
        }

        /// <summary>
        /// Stops the command handler, ending device discovery and saving configuration
        /// </summary>
        public void Stop()
        {
            // Save configuration
            _configManager.SaveConfiguration();
            
            // Stop device discovery
            _deviceManager.StopDeviceDiscovery();
            
            LogInfo("MonTilt stopped");
        }

        /// <summary>
        /// Handles a command input by the user
        /// </summary>
        /// <param name="command">The command to handle</param>
        public void HandleCommand(string command)
        {
            try
            {
                string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                string baseCommand = parts[0].ToLower();
                
                switch (baseCommand)
                {
                    case "help":
                        ShowHelp();
                        break;

                    case "install-service":
                        InstallService();
                        break;
                    
                    case "uninstall-service":
                        UninstallService();
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
                                    LogError($"Unknown list command: {subCommand}");
                                    LogInfo("Valid list commands: monitors, devices, mappings");
                                    break;
                            }
                        }
                        else
                        {
                            LogError("Missing list command parameter");
                            LogInfo("Usage: list [monitors|devices|mappings]");
                        }
                        break;
                    
                    case "map":
                        if (parts.Length >= 3 && int.TryParse(parts[2], out int monitorNumber))
                        {
                            string macAddress = parts[1].ToUpper();
                            MapDeviceToMonitor(macAddress, monitorNumber);
                        }
                        else
                        {
                            LogError("Invalid map command format");
                            LogInfo("Usage: map <mac> <monitor>");
                        }
                        break;
                    
                    case "unmap":
                        if (parts.Length >= 2)
                        {
                            string macAddress = parts[1].ToUpper();
                            UnmapDevice(macAddress);
                        }
                        else
                        {
                            LogError("Invalid unmap command format");
                            LogInfo("Usage: unmap <mac>");
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
                            string level = parts[1].ToLower();
                            SetLogLevel(level);
                        }
                        else
                        {
                            LogError("Invalid log command format");
                            LogInfo("Usage: log <level> (debug, info, warning, error)");
                        }
                        break;
                    
                    case "exit":
                        // Exit is handled by Program.cs
                        break;
                    
                    default:
                        LogError($"Unknown command: {baseCommand}");
                        LogInfo("Type 'help' for a list of commands");
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"Error executing command: {ex.Message}");
                LogDebug(ex.StackTrace ?? "No stack trace available");
            }
        }

        /// <summary>
        /// Shows the help text listing all available commands
        /// </summary>
        private void ShowHelp()
        {
            Console.WriteLine("\nMonTilt Commands:");
            Console.WriteLine("help                     - Show this help text");
            Console.WriteLine("list monitors            - List available monitors");
            Console.WriteLine("list devices             - List connected devices");
            Console.WriteLine("list mappings            - List device-to-monitor mappings");
            Console.WriteLine("install-service          - Install MonTilt as a Windows service");
            Console.WriteLine("uninstall-service        - Uninstall the MonTilt Windows service");
            Console.WriteLine("map <mac> <monitor>      - Map a device to a monitor");
            Console.WriteLine("unmap <mac>              - Remove a device mapping");
            Console.WriteLine("scan                     - Scan for new devices");
            Console.WriteLine("save                     - Save current configuration");
            Console.WriteLine("log <level>              - Set log level (debug, info, warning, error)");
            Console.WriteLine("exit                     - Exit the application");
            Console.WriteLine();
        }

        /// <summary>
        /// Lists all available monitors
        /// </summary>
        private void ListMonitors()
        {
            var monitors = _monitorDriver.GetMonitors();
            
            Console.WriteLine($"\nAvailable monitors ({monitors.Count}):");
            
            if (monitors.Count == 0)
            {
                Console.WriteLine("  No monitors detected");
                return;
            }
            
            for (int i = 0; i < monitors.Count; i++)
            {
                Console.WriteLine($"  {i + 1}. {monitors[i].DisplayName} ({monitors[i].Width}x{monitors[i].Height})");
            }
            
            Console.WriteLine();
        }

        /// <summary>
        /// Lists all connected devices
        /// </summary>
        private void ListDevices()
        {
            var devices = _deviceManager.GetConnectedDevices();
            
            Console.WriteLine($"\nConnected devices ({devices.Count}):");
            
            if (devices.Count == 0)
            {
                Console.WriteLine("  No devices connected");
                return;
            }
            
            foreach (var device in devices)
            {
                string monitorInfo = _deviceManager.GetDeviceMonitorMapping(device.MacAddress) is MappingStatus mapping && mapping.IsValid
                    ? $"mapped to monitor {mapping.MonitorIndex + 1}" 
                    : "not mapped to any monitor";
                    
                string orientationInfo = device.CurrentOrientation switch
                {
                    0 => "Landscape (0°)",
                    1 => "Portrait (90°)",
                    2 => "Landscape flipped (180°)",
                    3 => "Portrait flipped (270°)",
                    _ => $"Unknown ({device.CurrentOrientation}°)"
                };
                
                Console.WriteLine($"  {device.MacAddress} on {device.ComPort} - {monitorInfo} - {orientationInfo}");
            }
            
            Console.WriteLine();
        }

        /// <summary>
        /// Lists all device-to-monitor mappings
        /// </summary>
        private void ListMappings()
        {
            var mappings = _configManager.DeviceMonitorMap;
            
            Console.WriteLine($"\nDevice mappings ({mappings.Count}):");
            
            if (mappings.Count == 0)
            {
                Console.WriteLine("  No device mappings configured");
                return;
            }
            
            foreach (var mapping in mappings)
            {
                Console.WriteLine($"  Device {mapping.Key} -> Monitor {mapping.Value + 1}");
            }
            
            Console.WriteLine();
        }

        /// <summary>
        /// Maps a device to a monitor
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <param name="monitorNumber">The monitor number (1-based)</param>
        private void MapDeviceToMonitor(string macAddress, int monitorNumber)
        {
            var monitors = _monitorDriver.GetMonitors();
            
            if (monitorNumber < 1 || monitorNumber > monitors.Count)
            {
                LogError($"Invalid monitor number. Valid range: 1-{monitors.Count}");
                return;
            }
            
            int monitorIndex = monitorNumber - 1;
            
            // Update the mapping
            _deviceManager.MapDeviceToMonitor(macAddress, monitorIndex);
            
            LogInfo($"Mapped device {macAddress} to monitor {monitorNumber}");
        }

        /// <summary>
        /// Unmaps a device
        /// </summary>
        /// <param name="macAddress">The MAC address of the device to unmap</param>
        private void UnmapDevice(string macAddress)
        {
            if (_deviceManager.UnmapDevice(macAddress))
            {
                LogInfo($"Unmapped device {macAddress}");
            }
            else
            {
                LogWarning($"Device {macAddress} was not mapped to any monitor");
            }
        }

        /// <summary>
        /// Initiates a scan for new devices
        /// </summary>
        private void ScanForDevices()
        {
            LogInfo("Scanning for devices...");
            _deviceManager.ScanForDevices();
        }

        /// <summary>
        /// Saves the current configuration
        /// </summary>
        private void SaveConfiguration()
        {
            _configManager.SaveConfiguration();
            LogInfo("Configuration saved");
        }

        /// <summary>
        /// Sets the log level
        /// </summary>
        /// <param name="level">The log level to set</param>
        private void SetLogLevel(string level)
        {
            switch (level.ToLower())
            {
                case "debug":
                    _currentLogLevel = LogLevel.Debug;
                    LogInfo("Log level set to Debug");
                    break;
                case "info":
                    _currentLogLevel = LogLevel.Info;
                    LogInfo("Log level set to Info");
                    break;
                case "warning":
                    _currentLogLevel = LogLevel.Warning;
                    LogInfo("Log level set to Warning");
                    break;
                case "error":
                    _currentLogLevel = LogLevel.Error;
                    LogInfo("Log level set to Error");
                    break;
                default:
                    LogError($"Unknown log level: {level}");
                    LogInfo("Valid log levels: debug, info, warning, error");
                    break;
            }
        }

        /// <summary>
        /// Installs the MonTilt service
        /// </summary>
        private void InstallService()
        {
            try
            {
                // Check for admin rights
                bool isAdmin = IsRunningAsAdministrator();
                if (!isAdmin)
                {
                    LogError("Administrator privileges are required to install the service.");
                    LogInfo("Please run the CLI as administrator.");
                    return;
                }

                // Use AppContext.BaseDirectory instead of Assembly.Location
                string applicationPath = AppContext.BaseDirectory;
                string baseDirectory = Path.GetDirectoryName(applicationPath) ?? string.Empty;
                
                if (string.IsNullOrEmpty(baseDirectory))
                {
                    LogError("Could not determine application directory.");
                    return;
                }
                
                // Try to find the service executable
                string serviceExePath = Path.Combine(baseDirectory, "MonTilt.Service.exe");
                
                if (!File.Exists(serviceExePath))
                {
                    // Try alternate paths
                    string[] possiblePaths = new[]
                    {
                        Path.Combine(baseDirectory, "MonTilt.Service", "bin", "Debug", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "MonTilt.Service", "bin", "Release", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "..", "MonTilt.Service", "bin", "Debug", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "..", "MonTilt.Service", "bin", "Release", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "..", "..", "..", "..", "MonTilt.Service", "bin", "Debug", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "..", "..", "..", "..", "MonTilt.Service", "bin", "Release", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "MonTilt.Service", "bin", "Debug", "net6.0-windows", "MonTilt.Service.exe"),
                        Path.Combine(baseDirectory, "..", "..", "..", "..", "..", "MonTilt.Service", "bin", "Release", "net6.0-windows", "MonTilt.Service.exe")
                    };
                    
                    foreach (string path in possiblePaths)
                    {
                        if (File.Exists(path))
                        {
                            serviceExePath = path;
                            break;
                        }
                    }
                    
                    if (!File.Exists(serviceExePath))
                    {
                        LogError($"Service executable not found. Make sure you've built the MonTilt.Service project.");
                        LogInfo("Looking in: " + string.Join(", ", possiblePaths));
                        return;
                    }
                }
                
                LogInfo($"Found service executable at: {serviceExePath}");
                
                // Create process to run sc.exe
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"create MonTilt binPath= \"{serviceExePath}\" start= auto DisplayName= \"MonTilt Monitor Orientation Service\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                    
                // Start the process
                using (System.Diagnostics.Process? process = System.Diagnostics.Process.Start(psi))
                {
                    // Wait for the process to exit
                    process?.WaitForExit();
                        
                    // Check exit code
                    if (process?.ExitCode == 0)
                    {
                        LogInfo("MonTilt service installed successfully");
                            
                        // Start the service
                        System.Diagnostics.ProcessStartInfo startPsi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "sc.exe",
                            Arguments = "start MonTilt",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                            
                        using (System.Diagnostics.Process? startProcess = System.Diagnostics.Process.Start(startPsi))
                        {
                            startProcess?.WaitForExit();
                            LogInfo("MonTilt service started");
                        }
                    }
                    else
                    {
                        string error = process?.StandardError?.ReadToEnd() ?? "Unknown error";
                        LogError($"Failed to install service: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error installing service: {ex.Message}");
                LogDebug(ex.StackTrace ?? "No stack trace available");
            }
        }

        /// <summary>
        /// Uninstalls the MonTilt service
        /// </summary>
        private void UninstallService()
        {
            try
            {
                // Check for admin rights
                bool isAdmin = IsRunningAsAdministrator();
                if (!isAdmin)
                {
                    LogError("Administrator privileges are required to uninstall the service.");
                    LogInfo("Please run the CLI as administrator.");
                    return;
                }

                // Create process to stop the service
                System.Diagnostics.ProcessStartInfo stopPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "stop MonTilt",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                    
                // Stop the service
                using (System.Diagnostics.Process? stopProcess = System.Diagnostics.Process.Start(stopPsi))
                {
                    stopProcess?.WaitForExit();
                }
                    
                // Create process to run sc.exe to delete the service
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = "delete MonTilt",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                    
                // Start the process
                using (System.Diagnostics.Process? process = System.Diagnostics.Process.Start(psi))
                {
                    // Wait for the process to exit
                    process?.WaitForExit();
                        
                    // Check exit code
                    if (process?.ExitCode == 0)
                    {
                        LogInfo("MonTilt service uninstalled successfully");
                    }
                    else
                    {
                        string error = process?.StandardError?.ReadToEnd() ?? "Unknown error";
                        LogError($"Failed to uninstall service: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Error uninstalling service: {ex.Message}");
                LogDebug(ex.StackTrace ?? "No stack trace available");
            }
        }

        /// <summary>
        /// Checks if the application is running with administrator privileges
        /// </summary>
        /// <returns>True if running as administrator, false otherwise</returns>
        private bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        #region Logging Methods
        
        private void LogDebug(string message)
        {
            if (_currentLogLevel <= LogLevel.Debug)
            {
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($"[DEBUG] {message}");
                Console.ResetColor();
            }
        }
        
        private void LogInfo(string message)
        {
            if (_currentLogLevel <= LogLevel.Info)
            {
                Console.WriteLine(message);
            }
        }
        
        private void LogWarning(string message)
        {
            if (_currentLogLevel <= LogLevel.Warning)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[WARNING] {message}");
                Console.ResetColor();
            }
        }
        
        private void LogError(string message)
        {
            if (_currentLogLevel <= LogLevel.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] {message}");
                Console.ResetColor();
            }
        }
        
        #endregion
    }
}