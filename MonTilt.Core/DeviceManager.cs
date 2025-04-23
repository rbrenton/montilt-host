using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonTilt.Driver;

namespace MonTilt.Core
{
    /// <summary>
    /// Manages ESP32 device discovery, connection, and orientation updates
    /// </summary>
    public class DeviceManager
    {
        // Port scanning constants
        private const int PORT_SCAN_INTERVAL_MS = 10000; // 10 seconds
        private const int DEVICE_DETECTION_TIMEOUT_MS = 10000; // 10 seconds

        // Collection of connected devices
        private ConcurrentDictionary<string, ESP32Device> _connectedDevices = new ConcurrentDictionary<string, ESP32Device>();
        
        // Collection of ports being checked
        private ConcurrentDictionary<string, DateTime> _portsBeingChecked = new ConcurrentDictionary<string, DateTime>();
        
        // Reference to config manager
        private readonly ConfigManager _configManager;
        
        // Reference to monitor driver
        private readonly MonitorOrientationDriver _monitorDriver;
        
        // Background scanning task
        private Task? _backgroundScanTask;
        private CancellationTokenSource? _scanCancellationTokenSource;
        private bool _isRunning = false;

        /// <summary>
        /// Event raised when a device's orientation changes
        /// </summary>
        public event EventHandler<OrientationChangedEventArgs>? OrientationChanged;

        /// <summary>
        /// Event raised when a new device is discovered
        /// </summary>
        public event EventHandler<string>? DeviceDiscovered;

        /// <summary>
        /// Event raised when a device is connected
        /// </summary>
        public event EventHandler<ESP32Device>? DeviceConnected;

        /// <summary>
        /// Event raised when a device is disconnected
        /// </summary>
        public event EventHandler<string>? DeviceDisconnected;

        /// <summary>
        /// Constructor for DeviceManager
        /// </summary>
        /// <param name="configManager">The configuration manager</param>
        /// <param name="monitorDriver">The monitor orientation driver</param>
        public DeviceManager(ConfigManager configManager, MonitorOrientationDriver monitorDriver)
        {
            _configManager = configManager;
            _monitorDriver = monitorDriver;
            
            // Subscribe to configuration updates
            _configManager.ConfigurationUpdated += OnConfigurationUpdated;
        }

        /// <summary>
        /// Starts device discovery
        /// </summary>
        public void StartDeviceDiscovery()
        {
            if (_isRunning)
                return;
                
            _isRunning = true;
            
            // Connect to already known devices from config
            ConnectKnownDevices();
            
            // Start background scanning
            _scanCancellationTokenSource = new CancellationTokenSource();
            _backgroundScanTask = Task.Run(() => BackgroundScanLoop(_scanCancellationTokenSource.Token));
        }

        /// <summary>
        /// Stops device discovery
        /// </summary>
        public void StopDeviceDiscovery()
        {
            if (!_isRunning)
                return;
                
            _isRunning = false;
            
            // Stop background scanning
            _scanCancellationTokenSource?.Cancel();
            try
            {
                _backgroundScanTask?.Wait(1000);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancelling the task
            }
            catch (AggregateException ex) when (ex.InnerExceptions.Any(e => e is OperationCanceledException))
            {
                // Expected when cancelling the task
            }
            
            // Disconnect all devices
            DisconnectAllDevices();
        }

        /// <summary>
        /// Scans for new devices
        /// </summary>
        public void ScanForDevices()
        {
            // Get available ports
            string[] availablePorts = SerialPort.GetPortNames();
            
            Console.WriteLine($"Found {availablePorts.Length} COM port(s)");
            
            // Try to connect to ports that aren't already connected or being checked
            foreach (string port in availablePorts)
            {
                bool alreadyConnected = _connectedDevices.Values.Any(d => d.ComPort == port);
                bool alreadyBeingChecked = _portsBeingChecked.ContainsKey(port);
                
                if (!alreadyConnected && !alreadyBeingChecked)
                {
                    Console.WriteLine($"Checking {port}...");
                    TryConnectToPort(port, true);
                }
            }
            
            Console.WriteLine($"Scan complete. {_connectedDevices.Count} device(s) connected.");
        }

        /// <summary>
        /// Gets a list of all connected devices
        /// </summary>
        /// <returns>List of connected devices</returns>
        public List<ESP32Device> GetConnectedDevices()
        {
            return _connectedDevices.Values.ToList();
        }

        /// <summary>
        /// Gets the monitor mapping status for a device
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <returns>The mapping status, or null if the device is not mapped</returns>
        public MappingStatus? GetDeviceMonitorMapping(string macAddress)
        {
            if (_configManager.DeviceMonitorMap.TryGetValue(macAddress, out int monitorIndex))
            {
                // Verify that the monitor index is valid
                var monitors = _monitorDriver.GetMonitors();
                bool isValid = monitorIndex >= 0 && monitorIndex < monitors.Count;
                
                return new MappingStatus(macAddress, monitorIndex, isValid);
            }
            
            return null;
        }

        /// <summary>
        /// Maps a device to a monitor
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <param name="monitorIndex">The monitor index</param>
        public void MapDeviceToMonitor(string macAddress, int monitorIndex)
        {
            // Update the mapping in config
            _configManager.MapDeviceToMonitor(macAddress, monitorIndex);
            
            // If the device is connected, update its monitor index
            if (_connectedDevices.TryGetValue(macAddress, out ESP32Device? device))
            {
                device.MonitorIndex = monitorIndex;
                
                // Apply current orientation immediately if available
                ApplyDeviceOrientation(device);
            }
        }

        /// <summary>
        /// Unmaps a device from its monitor
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <returns>True if the device was unmapped, false if it wasn't mapped</returns>
        public bool UnmapDevice(string macAddress)
        {
            bool removed = _configManager.UnmapDevice(macAddress);
            
            // If the device is connected, update its monitor index
            if (removed && _connectedDevices.TryGetValue(macAddress, out ESP32Device? device))
            {
                device.MonitorIndex = -1;
            }
            
            return removed;
        }

        #region Private Methods

        /// <summary>
        /// Connects to devices that are already known from the configuration
        /// </summary>
        private void ConnectKnownDevices()
        {
            string[] availablePorts = SerialPort.GetPortNames();
            
            // Try to connect to known devices
            foreach (var kvp in _configManager.DevicePortMap)
            {
                string macAddress = kvp.Key;
                string portName = kvp.Value;
                
                // Check if the port is still available and device isn't already connected
                if (Array.IndexOf(availablePorts, portName) >= 0 && !_connectedDevices.ContainsKey(macAddress))
                {
                    Console.WriteLine($"Connecting to known device on {portName}...");
                    TryConnectToPort(portName, true, macAddress);
                }
            }
        }

        /// <summary>
        /// Background loop to periodically scan for new devices
        /// </summary>
        private async Task BackgroundScanLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Check for new ports
                    await BackgroundScanForDevices(cancellationToken);
                    
                    // Wait for the next scan interval
                    await Task.Delay(PORT_SCAN_INTERVAL_MS, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in background scan: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Performs a background scan for new devices
        /// </summary>
        private async Task BackgroundScanForDevices(CancellationToken cancellationToken)
        {
            string[] availablePorts = SerialPort.GetPortNames();
            
            // Look for new ports that aren't being checked and aren't already connected
            foreach (string port in availablePorts)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                    
                bool alreadyConnected = _connectedDevices.Values.Any(d => d.ComPort == port);
                bool alreadyBeingChecked = _portsBeingChecked.ContainsKey(port);
                
                if (!alreadyConnected && !alreadyBeingChecked)
                {
                    // Mark this port as being checked
                    _portsBeingChecked[port] = DateTime.Now;
                    
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
                }
            }
        }

        /// <summary>
        /// Tries to connect to a port to check for an ESP32 device
        /// </summary>
        /// <param name="portName">The port name to connect to</param>
        /// <param name="verbose">Whether to output verbose connection information</param>
        /// <param name="knownMacAddress">The known MAC address, if available</param>
        private void TryConnectToPort(string portName, bool verbose = true, string knownMacAddress = "")
        {
            // Don't try to connect if it's already connected
            if (_connectedDevices.Values.Any(d => d.ComPort == portName))
            {
                if (verbose)
                {
                    Console.WriteLine($"Port {portName} is already connected");
                }
                return;
            }
            
            // Create a new ESP32 device
            ESP32Device device = new ESP32Device(portName);
            
            try
            {
                // Set up event handlers
                SetupDeviceEventHandlers(device);
                
                // Try to open the port
                device.Open();
                
                // If we have a known MAC address, use it right away
                if (!string.IsNullOrEmpty(knownMacAddress))
                {
                    device.MacAddress = knownMacAddress;
                    device.IsIdentified = true;
                    
                    // Register in the device dictionary
                    _connectedDevices[knownMacAddress] = device;
                    
                    // Check if there's a monitor mapping
                    if (_configManager.DeviceMonitorMap.TryGetValue(knownMacAddress, out int monitorIndex))
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
                    }
                    
                    // Raise the connected event
                    DeviceConnected?.Invoke(this, device);
                }
                else
                {
                    // Register with a temporary key until we get the MAC address
                    string tempKey = $"temp_{portName}";
                    _connectedDevices[tempKey] = device;
                    
                    if (verbose)
                    {
                        Console.WriteLine($"Connected to {portName}, waiting for device identification...");
                    }
                    
                    // Start a timeout task to clean up if no identification is received
                    Task.Run(async () =>
                    {
                        // Wait for the device to be identified
                        await Task.Delay(DEVICE_DETECTION_TIMEOUT_MS);
                        
                        // If the device still hasn't been identified
                        if (!device.IsIdentified)
                        {
                            if (verbose)
                            {
                                Console.WriteLine($"No identification received from {portName} within timeout period");
                            }
                            
                            // Try to send a newline to prompt the device
                            try
                            {
                                if (device.IsOpen)
                                {
                                    device.WriteLineToDevice("");
                                }
                            }
                            catch { }
                            
                            // Wait a bit longer
                            await Task.Delay(2000);
                            
                            // If still not identified, clean up
                            if (!device.IsIdentified)
                            {
                                // Remove from devices dictionary
                                _connectedDevices.TryRemove(tempKey, out _);
                                
                                // Close the port
                                device.Close();
                                
                                if (verbose)
                                {
                                    Console.WriteLine($"Disconnected from {portName} (no ESP32 device detected)");
                                }
                            }
                        }
                    });
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

        /// <summary>
        /// Sets up event handlers for a device
        /// </summary>
        /// <param name="device">The device to set up event handlers for</param>
        private void SetupDeviceEventHandlers(ESP32Device device)
        {
            // MAC address received handler
            device.MacAddressReceived += (sender, mac) =>
            {
                // Remove the device from temp dictionary if it was there
                string tempKey = $"temp_{device.ComPort}";
                _connectedDevices.TryRemove(tempKey, out _);
                
                mac = mac.ToUpper();
                
                // Add to devices dictionary with proper MAC key
                _connectedDevices[mac] = device;
                
                // Update config maps
                _configManager.UpdateDevicePort(mac, device.ComPort);
                
                // Check if this device has a monitor mapping
                if (_configManager.DeviceMonitorMap.TryGetValue(mac, out int monitorIndex))
                {
                    device.MonitorIndex = monitorIndex;
                    Console.WriteLine($"Device {mac} identified on {device.ComPort} (mapped to monitor {monitorIndex + 1})");
                }
                else
                {
                    Console.WriteLine($"Device {mac} identified on {device.ComPort} (not mapped to any monitor)");
                }
                
                // Raise the discovered and connected events
                DeviceDiscovered?.Invoke(this, mac);
                DeviceConnected?.Invoke(this, device);
            };
            
            // Orientation changed handler
            device.OrientationChanged += (sender, data) =>
            {
                if (device.MonitorIndex >= 0)
                {
                    // Apply the new orientation
                    ApplyDeviceOrientation(device);
                    
                    // Raise the orientation changed event
                    OrientationChanged?.Invoke(this, new OrientationChangedEventArgs(
                        device.MacAddress, 
                        device.MonitorIndex, 
                        device.CurrentOrientation));
                }
            };
            
            // Disconnection handler
            device.Disconnected += (sender, args) =>
            {
                // Remove from devices dictionary
                _connectedDevices.TryRemove(device.MacAddress, out _);
                
                // Raise the disconnected event
                DeviceDisconnected?.Invoke(this, device.MacAddress);
            };
        }

        /// <summary>
        /// Applies a device's current orientation to its mapped monitor
        /// </summary>
        /// <param name="device">The device to apply orientation for</param>
        private void ApplyDeviceOrientation(ESP32Device device)
        {
            if (device.MonitorIndex < 0)
                return;
                
            // Get monitor info
            var monitors = _monitorDriver.GetMonitors();
            
            // Make sure the monitor index is valid
            if (device.MonitorIndex >= 0 && device.MonitorIndex < monitors.Count)
            {
                // Extract orientation from device
                Orientation orientation = (Orientation)device.CurrentOrientation;
                
                // Apply to monitor
                _monitorDriver.SetMonitorOrientation((uint) device.MonitorIndex, orientation);
            }
        }

        /// <summary>
        /// Disconnects all devices
        /// </summary>
        private void DisconnectAllDevices()
        {
            foreach (var device in _connectedDevices.Values.ToList())
            {
                device.Close();
            }
            
            _connectedDevices.Clear();
        }

        /// <summary>
        /// Handles configuration updates
        /// </summary>
        private void OnConfigurationUpdated(object? sender, EventArgs e)
        {
            // Apply any mapping changes to connected devices
            foreach (var device in _connectedDevices.Values)
            {
                if (_configManager.DeviceMonitorMap.TryGetValue(device.MacAddress, out int monitorIndex))
                {
                    if (device.MonitorIndex != monitorIndex)
                    {
                        device.MonitorIndex = monitorIndex;
                        ApplyDeviceOrientation(device);
                    }
                }
                else
                {
                    device.MonitorIndex = -1;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Event args for orientation changed events
    /// </summary>
    public class OrientationChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The MAC address of the device that changed orientation
        /// </summary>
        public string DeviceMac { get; }
        
        /// <summary>
        /// The monitor index
        /// </summary>
        public int MonitorIndex { get; }
        
        /// <summary>
        /// The new orientation
        /// </summary>
        public int Orientation { get; }
        
        /// <summary>
        /// Constructor for OrientationChangedEventArgs
        /// </summary>
        /// <param name="deviceMac">The MAC address of the device that changed orientation</param>
        /// <param name="monitorIndex">The monitor index</param>
        /// <param name="orientation">The new orientation</param>
        public OrientationChangedEventArgs(string deviceMac, int monitorIndex, int orientation)
        {
            DeviceMac = deviceMac;
            MonitorIndex = monitorIndex;
            Orientation = orientation;
        }
    }
}