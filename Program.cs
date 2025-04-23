using System;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text;
using System.Collections.Concurrent;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;

namespace MonitorOrientationManager
{
    class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, int dwflags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;

            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;

            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        // Display orientation values
        private const int DMDO_DEFAULT = 0;
        private const int DMDO_90 = 1;
        private const int DMDO_180 = 2;
        private const int DMDO_270 = 3;

        // Display settings flags
        private const int CDS_UPDATEREGISTRY = 0x01;
        private const int CDS_TEST = 0x02;
        private const int CDS_FULLSCREEN = 0x04;
        private const int CDS_GLOBAL = 0x08;
        private const int CDS_SET_PRIMARY = 0x10;
        private const int CDS_NORESET = 0x10000000;

        // Configuration file path
        private const string CONFIG_FILE = "monitor_config.json";
        
        // Auto-detection constants
        private const int BAUD_RATE = 115200;
        private const int DETECTION_TIMEOUT_MS = 10000; // Increased timeout
        private const string ESP32_IDENTIFIER = "mac";

        // Constants for display settings
        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int DM_PELSWIDTH = 0x00080000;
        private const int DM_PELSHEIGHT = 0x00100000; 
        private const int DM_DISPLAYORIENTATION = 0x00800000;

        // Constants for display change results
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const int DISP_CHANGE_RESTART = 1;
        private const int DISP_CHANGE_FAILED = -1;
        private const int DISP_CHANGE_BADMODE = -2;
        private const int DISP_CHANGE_NOTUPDATED = -3;
        private const int DISP_CHANGE_BADFLAGS = -4;
        private const int DISP_CHANGE_BADPARAM = -5;

        // Class to store monitor configuration
        class MonitorConfig
        {
            public Dictionary<string, int> DeviceMonitorMap { get; set; } = new Dictionary<string, int>();
            public Dictionary<string, string> DevicePortMap { get; set; } = new Dictionary<string, string>();
        }

        // Class to manage a connected ESP32 device
        class ESP32Device
        {
            public string ComPort { get; set; }
            public string MacAddress { get; set; }
            public SerialPort Port { get; set; }
            public int CurrentOrientation { get; set; } = 0;
            public int MonitorIndex { get; set; } = -1;
            public bool IsIdentified { get; set; } = false;

            // Buffer for storing incoming data until a complete line is received
            private StringBuilder _dataBuffer = new StringBuilder();
            private const int MAX_BUFFER_SIZE = 8192; // Add a max buffer size to prevent memory issues

            public ESP32Device(string comPort)
            {
                ComPort = comPort;
                MacAddress = string.Empty;
                Port = new SerialPort(comPort, BAUD_RATE);
                Port.DataReceived += Port_DataReceived;
                Port.Encoding = Encoding.UTF8; // Ensure correct encoding
                
                // Increase buffer sizes for more reliability
                Port.ReadBufferSize = 4096;
                Port.WriteBufferSize = 4096;
            }

            private void Port_DataReceived(object sender, SerialDataReceivedEventArgs e)
            {
                if (sender is SerialPort sp && sp.IsOpen) // Check if port is still open
                {
                    try
                    {
                        // Read all available data as string
                        int bytesToRead = sp.BytesToRead;
                        if (bytesToRead > 0)
                        {
                            byte[] buffer = new byte[bytesToRead];
                            int bytesRead = sp.Read(buffer, 0, bytesToRead);
                            string receivedData = Port.Encoding.GetString(buffer, 0, bytesRead);

                            // Append to the internal buffer
                            _dataBuffer.Append(receivedData);

                            // Prevent buffer from growing indefinitely
                            if (_dataBuffer.Length > MAX_BUFFER_SIZE)
                            {
                                Console.WriteLine($"Warning: Data buffer for {ComPort} exceeded max size. Clearing buffer.");
                                _dataBuffer.Clear();
                                // Optionally, keep the last part of the buffer if needed
                                // _dataBuffer.Remove(0, _dataBuffer.Length - MAX_BUFFER_SIZE / 2);
                            }

                            // Process the buffer for complete messages
                            ProcessBuffer();
                        }
                    }
                    catch (TimeoutException) { /* Ignore read timeouts */ }
                    catch (IOException ex)
                    {
                        Console.WriteLine($"IO Error reading from {ComPort}: {ex.Message}. Closing port.");
                        Close(); // Close port on IO error
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading from {ComPort}: {ex.Message}");
                        // Consider closing the port here too depending on the error
                    }
                }
            }
            
            private void ProcessBuffer()
            {
                string bufferContent = _dataBuffer.ToString();
                int messageStartIndex = 0;

                while (messageStartIndex < bufferContent.Length)
                {
                    // Trim leading whitespace/newlines from the search start
                    while (messageStartIndex < bufferContent.Length && char.IsWhiteSpace(bufferContent[messageStartIndex]))
                    {
                        messageStartIndex++;
                    }
                    if (messageStartIndex >= bufferContent.Length) break; // No more content

                    string? completeMessage = null;
                    int messageEndIndex = -1;

                    // Check for JSON object start
                    if (bufferContent[messageStartIndex] == '{')
                    {
                        int braceBalance = 0;
                        int searchIndex = messageStartIndex;
                        while (searchIndex < bufferContent.Length)
                        {
                            if (bufferContent[searchIndex] == '{') braceBalance++;
                            else if (bufferContent[searchIndex] == '}') braceBalance--;

                            if (braceBalance == 0 && bufferContent[searchIndex] == '}')
                            {
                                // Found a complete JSON object
                                messageEndIndex = searchIndex;
                                completeMessage = bufferContent.Substring(messageStartIndex, messageEndIndex - messageStartIndex + 1);
                                break;
                            }
                            searchIndex++;
                        }
                        // If braceBalance != 0, the JSON is incomplete, wait for more data
                    }
                    else
                    {
                        // Unexpected data start, try to find the next left curly brace or potential MAC start
                        Console.WriteLine($"Unexpected data start on {ComPort}: {bufferContent[messageStartIndex]}");
                        // Skip this character and continue search
                        messageStartIndex++;
                        continue; // Go to next iteration of the while loop
                    }


                    // If a complete message was found
                    if (completeMessage != null && messageEndIndex != -1)
                    {
                        ProcessLine(completeMessage); // Process the extracted message
                        // Remove the processed message (and any leading whitespace) from the buffer
                        _dataBuffer.Remove(0, messageEndIndex + 1);
                        bufferContent = _dataBuffer.ToString(); // Update buffer content string
                        messageStartIndex = 0; // Reset search index for the next message
                    }
                    else
                    {
                        // No complete message found starting at messageStartIndex, break and wait for more data
                        break;
                    }
                }
                // Optional: Trim the beginning of the buffer if it's just whitespace
                if (_dataBuffer.Length > 0 && char.IsWhiteSpace(_dataBuffer[0]))
                {
                    string trimmed = _dataBuffer.ToString().TrimStart();
                    _dataBuffer.Clear().Append(trimmed);
                }
            }
            
            // ProcessLine remains mostly the same, but receives a guaranteed complete message
            private void ProcessLine(string message)
            {
                // For debugging
                Console.WriteLine($"Processing message from {ComPort}: {message}");

                // Try to parse as JSON
                try
                {
                    // Check if it looks like a JSON object (already checked in ProcessBuffer, but good practice)
                    if (message.StartsWith("{") && message.EndsWith("}"))
                    {
                        AdvancedOrientationData? orientationData = JsonSerializer.Deserialize<AdvancedOrientationData>(message);

                        if (orientationData != null && !string.IsNullOrEmpty(orientationData.Mac))
                        {
                            // Update MAC address if not set yet (should be identified by now, but as fallback)
                            if (!IsIdentified)
                            {
                                MacAddress = orientationData.Mac;
                                IsIdentified = true;
                                Console.WriteLine($"Identified MAC {MacAddress} for {ComPort} via JSON");
                                OnMacAddressReceived?.Invoke(this, MacAddress);
                            }
                            else if (MacAddress != orientationData.Mac)
                            {
                                // Optional: Handle MAC mismatch if needed
                                Console.WriteLine($"Warning: Received JSON MAC {orientationData.Mac} differs from identified MAC {MacAddress} on {ComPort}");
                            }

                            // Process orientation data
                            if (MonitorIndex >= 0 && orientationData.Orientation != CurrentOrientation)
                            {
                                CurrentOrientation = orientationData.Orientation;
                                OnOrientationChanged?.Invoke(this, orientationData);
                            }
                        }
                        else if (orientationData == null) {
                            Console.WriteLine($"Failed to deserialize JSON: {message}");
                        }
                        else if (string.IsNullOrEmpty(orientationData.Mac)) {
                            Console.WriteLine($"JSON message missing MAC address: {message}");
                        }
                    }
                }
                catch (JsonException jsonEx)
                {
                    // Not valid JSON data, could be debug output or noise
                    Console.WriteLine($"JSON Parsing Error on {ComPort}: {jsonEx.Message}. Data: {message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error processing message from {ComPort}: {ex.Message}. Data: {message}");
                }
            }

            public void Close()
            {
                if (Port != null)
                {
                    // Unsubscribe event handler to prevent issues during closing
                    Port.DataReceived -= Port_DataReceived;

                    if (Port.IsOpen)
                    {
                        try
                        {
                            Port.Close();
                        }
                        catch (IOException ex)
                        {
                            // Handle potential errors during close (e.g., port disconnected)
                            Console.WriteLine($"Error closing port {ComPort}: {ex.Message}");
                        }
                    }
                    Port.Dispose();
                    // Port = null; // Optional: Set to null after disposal
                }
            }

            // Events
            public event EventHandler<string> OnMacAddressReceived;
            public event EventHandler<AdvancedOrientationData> OnOrientationChanged;
        }

        static MonitorConfig _config = new MonitorConfig();
        static ConcurrentDictionary<string, ESP32Device> _devices = new ConcurrentDictionary<string, ESP32Device>();
        static bool _running = true;
        static ConcurrentDictionary<string, DateTime> _portsBeingChecked = new ConcurrentDictionary<string, DateTime>();

        static void Main(string[] args)
        {
            // Check if running on Windows
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Error: This application is only supported on Windows.");
                Console.WriteLine("System.IO.Ports is currently only supported on Windows.");
                return;
            }

            try
            {
                // Load or create configuration
                LoadOrCreateConfig();

                // Get all display devices
                Screen[] screens = Screen.AllScreens;
                if (screens.Length < 1)
                {
                    Console.WriteLine("Error: No monitors detected.");
                    return;
                }

                Console.WriteLine($"Detected {screens.Length} monitor(s)");
                for (int i = 0; i < screens.Length; i++)
                {
                    Console.WriteLine($"{i + 1}. {screens[i].DeviceName}");
                }
                
                // Auto-detect and connect to ESP32 devices
                AutoDetectESP32Devices();
                
                Console.WriteLine("\nCommands:");
                Console.WriteLine("  map [mac address] [monitor number] - Map device to monitor");
                Console.WriteLine("  list - List current device mappings");
                Console.WriteLine("  scan - Rescan for new ESP32 devices");
                Console.WriteLine("  save - Save current configuration");
                Console.WriteLine("  exit - Exit the application");
                
                // Start a background thread to periodically check for new devices
                Thread backgroundScanThread = new Thread(() => {
                    while (_running)
                    {
                        // Sleep for 10 seconds between background scans
                        Thread.Sleep(10000);
                        
                        // Perform a quiet background scan
                        BackgroundScanForDevices();
                    }
                });
                backgroundScanThread.IsBackground = true;
                backgroundScanThread.Start();
                
                // Command processing loop
                while (_running)
                {
                    string? command = Console.ReadLine()?.Trim().ToLower();
                    
                    if (string.IsNullOrEmpty(command))
                        continue;
                        
                    if (command == "exit")
                    {
                        _running = false;
                    }
                    else if (command == "list")
                    {
                        ListDevicesAndMappings();
                    }
                    else if (command == "scan")
                    {
                        AutoDetectESP32Devices();
                    }
                    else if (command == "save")
                    {
                        SaveConfig();
                        Console.WriteLine("Configuration saved.");
                    }
                    else if (command.StartsWith("map "))
                    {
                        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 3 && int.TryParse(parts[2], out int monitorNumber))
                        {
                            string macAddress = parts[1].ToUpper();
                            if (monitorNumber < 1 || monitorNumber > screens.Length)
                            {
                                Console.WriteLine($"Invalid monitor number. Valid range: 1-{screens.Length}");
                            }
                            else
                            {
                                _config.DeviceMonitorMap[macAddress] = monitorNumber - 1;
                                
                                // Update the device monitor index if connected
                                foreach (var device in _devices.Values)
                                {
                                    if (device.MacAddress == macAddress)
                                    {
                                        device.MonitorIndex = monitorNumber - 1;
                                        break;
                                    }
                                }
                                
                                Console.WriteLine($"Mapped device {macAddress} to monitor {monitorNumber}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Invalid command format. Use: map [mac address] [monitor number]");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unknown command. Available commands: map, list, scan, save, exit");
                    }
                }
            }
            catch (PlatformNotSupportedException ex)
            {
                Console.WriteLine($"Platform Error: {ex.Message}");
                Console.WriteLine("This application requires Windows to run.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                // Clean up all devices
                foreach (var device in _devices.Values)
                {
                    device.Close();
                }
                
                // Save configuration before exiting
                SaveConfig();
            }
        }

        // Quieter background scanning to check for new devices
        static void BackgroundScanForDevices()
        {
            try
            {
                string[] availablePorts = SerialPort.GetPortNames();
                
                // Look for new ports that aren't being checked and aren't already connected
                foreach (string port in availablePorts)
                {
                    bool alreadyConnected = _devices.Values.Any(d => d.ComPort == port);
                    bool alreadyBeingChecked = _portsBeingChecked.ContainsKey(port);
                    
                    if (!alreadyConnected && !alreadyBeingChecked)
                    {
                        // Mark this port as being checked
                        _portsBeingChecked[port] = DateTime.Now;
                        
                        // Start a background task to check this port
                        Thread checkThread = new Thread(() => {
                            try
                            {
                                // Check if this port has an ESP32
                                TryConnectToPort(port, false);
                            }
                            finally
                            {
                                // Remove this port from the being checked list
                                _portsBeingChecked.TryRemove(port, out _);
                            }
                        });
                        
                        checkThread.IsBackground = true;
                        checkThread.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                // Quiet error during background scan
                Console.WriteLine($"Background scan error: {ex.Message}");
            }
        }

        static void AutoDetectESP32Devices()
        {
            Console.WriteLine("Scanning for ESP32 devices...");
            
            // Get currently available COM ports
            string[] availablePorts = SerialPort.GetPortNames();
            
            if (availablePorts.Length == 0)
            {
                Console.WriteLine("No COM ports found. Please connect an ESP32 device.");
                return;
            }
            
            Console.WriteLine($"Found {availablePorts.Length} COM port(s)");
            
            // First, connect to already known devices from config
            foreach (var kvp in _config.DevicePortMap)
            {
                string macAddress = kvp.Key;
                string portName = kvp.Value;
                
                // Check if the port is still available
                if (Array.IndexOf(availablePorts, portName) >= 0 && !_devices.ContainsKey(macAddress))
                {
                    try
                    {
                        Console.WriteLine($"Connecting to known device on {portName}...");
                        TryConnectToPort(portName, true, macAddress);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to connect to known device on {portName}: {ex.Message}");
                    }
                }
            }
            
            // Next, try to connect to unknown ports
            foreach (string port in availablePorts)
            {
                bool alreadyConnected = _devices.Values.Any(d => d.ComPort == port);
                bool alreadyBeingChecked = _portsBeingChecked.ContainsKey(port);
                
                if (!alreadyConnected && !alreadyBeingChecked)
                {
                    try
                    {
                        Console.WriteLine($"Checking {port}...");
                        TryConnectToPort(port, true);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error checking {port}: {ex.Message}");
                    }
                }
            }
            
            Console.WriteLine($"Scan complete. {_devices.Count} ESP32 device(s) connected.");
        }
        
        static void TryConnectToPort(string portName, bool verbose = true, string knownMacAddress = "")
        {
            // Don't try to connect if it's already connected
            if (_devices.Values.Any(d => d.ComPort == portName))
            {
                if (verbose)
                {
                    Console.WriteLine($"Port {portName} is already connected");
                }
                return;
            }
            
            // Create a new ESP32 device
            ESP32Device device = new ESP32Device(portName);
            
            // Set up event handlers
            SetupDeviceEventHandlers(device);
            
            try
            {
                // Try to open the port
                device.Port.Open();
                
                // If we have a known MAC address, use it right away
                if (!string.IsNullOrEmpty(knownMacAddress))
                {
                    device.MacAddress = knownMacAddress;
                    device.IsIdentified = true;
                    
                    // Register in the device dictionary
                    _devices[knownMacAddress] = device;
                    
                    // Check if there's a monitor mapping
                    if (_config.DeviceMonitorMap.TryGetValue(knownMacAddress, out int monitorIndex))
                    {
                        device.MonitorIndex = monitorIndex;
                        if (verbose)
                        {
                            Console.WriteLine($"Connected to device {knownMacAddress} on {portName} (mapped to monitor {monitorIndex + 1})");
                        }
                    }
                    else if (verbose)
                    {
                        Console.WriteLine($"Connected to device {knownMacAddress} on {portName} (not mapped to any monitor)");
                        Console.WriteLine($"To map this device, type: map {knownMacAddress} [monitor number]");
                    }
                }
                else
                {
                    // Register with a temporary key until we get the MAC address
                    string tempKey = $"temp_{portName}";
                    _devices[tempKey] = device;
                    
                    // Wait for device identification (happens asynchronously via event handler)
                    if (verbose)
                    {
                        Console.WriteLine($"Connected to {portName}, waiting for device identification...");
                    }
                    
                    // Start a timeout thread to clean up if no identification is received
                    Thread timeoutThread = new Thread(() => {
                        // Wait for the device to be identified
                        Thread.Sleep(DETECTION_TIMEOUT_MS);
                        
                        // If the device still hasn't been identified
                        if (device.IsIdentified == false)
                        {
                            if (verbose)
                            {
                                Console.WriteLine($"No identification received from {portName} within timeout period");
                            }
                            
                            // Try to send a newline to prompt the device
                            try
                            {
                                if (device.Port.IsOpen)
                                {
                                    device.Port.WriteLine("");
                                }
                            }
                            catch { }
                            
                            // Wait a bit longer
                            Thread.Sleep(2000);
                            
                            // If still not identified, clean up
                            if (device.IsIdentified == false)
                            {
                                // Remove from devices dictionary
                                _devices.TryRemove(tempKey, out _);
                                
                                // Close the port
                                device.Close();
                                
                                if (verbose)
                                {
                                    Console.WriteLine($"Disconnected from {portName} (no ESP32 device detected)");
                                }
                            }
                        }
                    });
                    
                    timeoutThread.IsBackground = true;
                    timeoutThread.Start();
                }
            }
            catch (Exception ex)
            {
                device.Close();
                if (verbose)
                {
                    Console.WriteLine($"Error connecting to {portName}: {ex.Message}");
                }
            }
        }
        
        static void SetupDeviceEventHandlers(ESP32Device device)
        {
            // MAC address received handler
            device.OnMacAddressReceived += (sender, mac) => {
                if (sender is ESP32Device dev)
                {
                    // Remove the device from temp dictionary if it was there
                    string tempKey = $"temp_{dev.ComPort}";
                    _devices.TryRemove(tempKey, out _);
                    
                    mac = mac.ToUpper();

                    // Add to devices dictionary with proper MAC key
                    _devices[mac] = dev;
                    
                    // Update config maps
                    _config.DevicePortMap[mac] = dev.ComPort;
                    
                    // Check if this device has a monitor mapping
                    if (_config.DeviceMonitorMap.TryGetValue(mac, out int monitorIndex))
                    {
                        dev.MonitorIndex = monitorIndex;
                        Console.WriteLine($"Device {mac} on {dev.ComPort} is mapped to monitor {monitorIndex + 1}");
                    }
                    else
                    {
                        Console.WriteLine($"New device detected: {mac} on {dev.ComPort}");
                        Console.WriteLine($"To map this device, type: map {mac} [monitor number]");
                    }
                }
            };
            
            // Orientation changed handler
            device.OnOrientationChanged += (sender, data) => {
                if (sender is ESP32Device dev && dev.MonitorIndex >= 0)
                {
                    // Get all screen info
                    Screen[] screens = Screen.AllScreens;

                    // Make sure the monitor index is valid
                    if (dev.MonitorIndex >= 0 && dev.MonitorIndex < screens.Length)
                    {
                        // Get the screen device name
                        string screenName = screens[dev.MonitorIndex].DeviceName;

                        // Rotate screen on UI thread to avoid cross-thread issues
                        using (Form form = new Form())
                        {
                            // *** FIX: Force handle creation before calling Invoke ***
                            IntPtr handle = form.Handle; // Access the Handle property

                            form.Invoke((MethodInvoker)delegate {
                                RotateScreen(screenName, data.Orientation);

                                // Print orientation description
                                string orientationDesc = data.Orientation switch
                                {
                                    0 => "Landscape (0°)",
                                    1 => "Portrait - Right (90°)",
                                    2 => "Landscape - Upside Down (180°)",
                                    3 => "Portrait - Left (270°)",
                                    _ => $"Unknown ({data.Orientation}°)"
                                };

                                Console.WriteLine($"Monitor {dev.MonitorIndex + 1} changed to {orientationDesc}");
                            });
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Invalid monitor index: {dev.MonitorIndex}");
                    }
                }
            };

        }

        static void RotateScreen(string screenName, int orientation)
        {
            Console.WriteLine($"Attempting to rotate screen using display settings utility for orientation {orientation}");
            
            try
            {
                // Extract display number from the screen name
                int displayNumber = 1; // Default to first display
                if (screenName.StartsWith("\\\\.\\DISPLAY"))
                {
                    string numberPart = screenName.Substring(11); // Skip "\\\\.\\DISPLAY"
                    if (int.TryParse(numberPart, out int parsed))
                    {
                        displayNumber = parsed;
                    }
                }
                
                // Map our orientation values to the displayswitch.exe orientation values
                string orientationArg;
                switch (orientation)
                {
                    case 0: // Landscape
                        orientationArg = "/d";
                        break;
                    case 1: // Portrait (90 degrees)
                        orientationArg = "/90";
                        break;
                    case 2: // Landscape flipped (180 degrees)
                        orientationArg = "/180";
                        break;
                    case 3: // Portrait flipped (270 degrees)
                        orientationArg = "/270";
                        break;
                    default:
                        Console.WriteLine($"Invalid orientation: {orientation}");
                        return;
                }
                
                // Create process info to run the DisplaySwitch utility
                System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "displayswitch.exe",
                    Arguments = orientationArg,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                // Start the process
                Console.WriteLine($"Running displayswitch.exe {orientationArg}");
                using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi))
                {
                    // Wait for the process to exit
                    process.WaitForExit();
                    
                    // Check exit code
                    if (process.ExitCode == 0)
                    {
                        Console.WriteLine("Display rotation command executed successfully");
                    }
                    else
                    {
                        Console.WriteLine($"Display rotation failed with exit code: {process.ExitCode}");
                        Console.WriteLine($"Error output: {process.StandardError.ReadToEnd()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void LoadOrCreateConfig()
        {
            if (File.Exists(CONFIG_FILE))
            {
                try
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    MonitorConfig? loadedConfig = JsonSerializer.Deserialize<MonitorConfig>(json);
                    if (loadedConfig != null)
                    {
                        _config = loadedConfig;
                        
                        // Initialize empty collections if they don't exist
                        if (_config.DeviceMonitorMap == null)
                            _config.DeviceMonitorMap = new Dictionary<string, int>();
                            
                        if (_config.DevicePortMap == null)
                            _config.DevicePortMap = new Dictionary<string, string>();
                            
                        Console.WriteLine("Configuration loaded.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading configuration: {ex.Message}");
                    Console.WriteLine("Using default configuration.");
                    _config = new MonitorConfig();
                }
            }
            else
            {
                Console.WriteLine("No configuration file found. Using default configuration.");
                _config = new MonitorConfig();
            }
        }
        
        static void SaveConfig()
        {
            try
            {
                string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
            }
        }
        
        static void ListDevicesAndMappings()
        {
            Console.WriteLine("\nConnected ESP32 devices:");
            if (_devices.Count == 0)
            {
                Console.WriteLine("  No devices connected");
            }
            else
            {
                foreach (var device in _devices.Values)
                {
                    // Skip temporary entries
                    if (device.MacAddress.StartsWith("temp_"))
                        continue;
                        
                    string monitorInfo = device.MonitorIndex >= 0 ? 
                        $"mapped to monitor {device.MonitorIndex + 1}" : 
                        "not mapped to any monitor";
                    
                    Console.WriteLine($"  {device.MacAddress} on {device.ComPort} - {monitorInfo}");
                }
            }
            
            Console.WriteLine("\nSaved device mappings:");
            if (_config.DeviceMonitorMap.Count == 0)
            {
                Console.WriteLine("  No device mappings saved");
            }
            else
            {
                foreach (var mapping in _config.DeviceMonitorMap)
                {
                    Console.WriteLine($"  Device {mapping.Key} -> Monitor {mapping.Value + 1}");
                }
            }
        }
    }
    
    // Updated data class for advanced orientation data with MAC address
    class AdvancedOrientationData
    {
        [JsonPropertyName("mac")]
        public string? Mac { get; set; }

        [JsonPropertyName("orientation")]
        public int Orientation { get; set; }

        // Add other fields if they exist in the JSON
        [JsonPropertyName("x")]
        public float? X { get; set; }

        [JsonPropertyName("y")]
        public float? Y { get; set; }

        [JsonPropertyName("z")]
        public float? Z { get; set; }
    }
}