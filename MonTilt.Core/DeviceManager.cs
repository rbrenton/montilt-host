using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using MonTilt.Driver;

namespace MonTilt.Core
{
    /// <summary>
    /// Manages ESP32 devices and their connections
    /// </summary>
    public class DeviceManager : IDisposable
    {
        // Constants
        private const int DETECTION_TIMEOUT_MS = 10000;
        
        // Dictionary of connected devices (by MAC address)
        private readonly ConcurrentDictionary<string, ESP32Device> _devices = new ConcurrentDictionary<string, ESP32Device>();
        
        // Dictionary of temporary devices (by COM port) that are not yet identified
        private readonly ConcurrentDictionary<string, ESP32Device> _tempDevices = new ConcurrentDictionary<string, ESP32Device>();
        
        // Dictionary of device mappings (device MAC to monitor device name)
        private readonly ConcurrentDictionary<string, string> _deviceMonitorMap = new ConcurrentDictionary<string, string>();
        
        // Dictionary of ports being checked to avoid duplicate checks
        private readonly ConcurrentDictionary<string, DateTime> _portsBeingChecked = new ConcurrentDictionary<string, DateTime>();
        
        // Monitor orientation driver
        private readonly MonitorOrientationDriver _driver;
        
        // Logger
        private readonly Action<string> _logAction;
        
        // Background scanning thread
        private Thread _scanThread;
        private bool _scanning = false;
        private bool _running = true;
        
        // Events
        public event EventHandler<ESP32Device> DeviceConnected;
        public event EventHandler<string> DeviceDisconnected;
        public event EventHandler<(ESP32Device Device, Orientation Orientation)> OrientationChanged;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public DeviceManager(MonitorOrientationDriver driver, Action<string> logAction = null)
        {
            _driver = driver;
            _logAction = logAction ?? (s => { });
        }
        
        /// <summary>
        /// Dispose the device manager and release resources
        /// </summary>
        public void Dispose()
        {
            _running = false;
            
            // Stop the scan thread
            _scanThread?.Join(1000);
            
            // Close all devices
            foreach (var device in _devices.Values.Concat(_tempDevices.Values))
            {
                device.Dispose();
            }
            
            _devices.Clear();
            _tempDevices.Clear();
        }
        
        /// <summary>
        /// Start device discovery
        /// </summary>
        public void StartDeviceDiscovery()
        {
            if (_scanning) return;
            
            _scanning = true;
            _running = true;
            
            _scanThread = new Thread(() =>
            {
                while (_running && _scanning)
                {
                    ScanForDevices();
                    Thread.Sleep(5000); // Scan every 5 seconds
                }
            });
            
            _scanThread.IsBackground = true;
            _scanThread.Start();
            
            _logAction("Device discovery started");
        }
        
        /// <summary>
        /// Stop device discovery
        /// </summary>
        public void StopDeviceDiscovery()
        {
            _scanning = false;
            _logAction("Device discovery stopped");
        }
        
        /// <summary>
        /// Get a list of all connected devices
        /// </summary>
        public IReadOnlyList<ESP32Device> GetConnectedDevices()
        {
            return _devices.Values.ToList();
        }
        
        /// <summary>
        /// Get the status of all mappings
        /// </summary>
        public IReadOnlyList<MappingStatus> GetMappingStatus()
        {
            var status = new List<MappingStatus>();
            
            foreach (var mapping in _deviceMonitorMap)
            {
                string deviceMac = mapping.Key;
                string monitorDeviceName = mapping.Value;
                
                bool isConnected = _devices.TryGetValue(deviceMac, out ESP32Device device);
                
                status.Add(new MappingStatus
                {
                    DeviceMac = deviceMac,
                    MonitorDeviceName = monitorDeviceName,
                    IsConnected = isConnected,
                    CurrentOrientation = isConnected ? device.CurrentOrientation : Orientation.Landscape,
                    LastUpdated = DateTime.Now
                });
            }
            
            return status;
        }
        
        /// <summary>
        /// Map a device to a monitor
        /// </summary>
        public bool MapDeviceToMonitor(string deviceMac, string monitorDeviceName)
        {
            // Make sure the monitor exists
            if (!_driver.GetAvailableMonitors().Contains(monitorDeviceName))
            {
                return false;
            }
            
            // Add the mapping
            _deviceMonitorMap[deviceMac] = monitorDeviceName;
            
            // If the device is connected, check its current orientation
            if (_devices.TryGetValue(deviceMac, out ESP32Device device))
            {
                // Update the monitor orientation if needed
                _driver.SetMonitorOrientation(monitorDeviceName, device.CurrentOrientation);
            }
            
            return true;
        }
        
        /// <summary>
        /// Unmap a device
        /// </summary>
        public bool UnmapDevice(string deviceMac)
        {
            return _deviceMonitorMap.TryRemove(deviceMac, out _);
        }
        
        /// <summary>
        /// Load mappings from a config manager
        /// </summary>
        public void LoadMappings(Dictionary<string, string> mappings)
        {
            foreach (var mapping in mappings)
            {
                _deviceMonitorMap[mapping.Key] = mapping.Value;
            }
        }
        
        /// <summary>
        /// Get current mappings
        /// </summary>
        public Dictionary<string, string> GetMappings()
        {
            return new Dictionary<string, string>(_deviceMonitorMap);
        }
        
        /// <summary>
        /// Scan for new devices
        /// </summary>
        private void ScanForDevices()
        {
            // Get all available COM ports
            string[] availablePorts = SerialPort.GetPortNames();
            
            // Check if any devices have been disconnected
            foreach (var device in _devices.Values.ToList())
            {
                if (!availablePorts.Contains(device.ComPort) || !device.Port.IsOpen)
                {
                    // Remove the device
                    if (_devices.TryRemove(device.MacAddress, out _))
                    {
                        device.Dispose();
                        DeviceDisconnected?.Invoke(this, device.MacAddress);
                        _logAction($"Device {device.MacAddress} on {device.ComPort} disconnected");
                    }
                }
            }
            
            // Check for new ports
            foreach (string port in availablePorts)
            {
                bool alreadyConnected = _devices.Values.Any(d => d.ComPort == port) || 
                                      _tempDevices.Values.Any(d => d.ComPort == port);
                bool alreadyBeingChecked = _portsBeingChecked.ContainsKey(port);
                
                if (!alreadyConnected && !alreadyBeingChecked)
                {
                    // Mark port as being checked
                    _portsBeingChecked[port] = DateTime.Now;
                    
                    // Start a thread to check this port
                    Thread checkThread = new Thread(() => CheckPort(port));
                    checkThread.IsBackground = true;
                    checkThread.Start();
                }
            }
        }
        
        /// <summary>
        /// Check if a port has an ESP32 device
        /// </summary>
        private void CheckPort(string port)
        {
            try
            {
                _logAction($"Checking port {port}...");
                
                // Create a device
                ESP32Device device = new ESP32Device(port);
                
                // Set up event handlers
                device.MacAddressReceived += (sender, mac) =>
                {
                    if (sender is ESP32Device dev)
                    {
                        // Remove from temp devices
                        _tempDevices.TryRemove(dev.ComPort, out _);
                        
                        // Add to devices with MAC address as key
                        _devices[mac] = dev;
                        
                        // Check if there's a mapping for this device
                        if (_deviceMonitorMap.TryGetValue(mac, out string monitorDeviceName))
                        {
                            // Update the monitor orientation
                            _driver.SetMonitorOrientation(monitorDeviceName, dev.CurrentOrientation);
                        }
                        
                        // Raise event
                        DeviceConnected?.Invoke(this, dev);
                        
                        _logAction($"Device {mac} connected on {dev.ComPort}");
                    }
                };
                
                device.OrientationChanged += (sender, data) =>
                {
                    if (sender is ESP32Device dev && _deviceMonitorMap.TryGetValue(dev.MacAddress, out string monitorDeviceName))
                    {
                        // Update the monitor orientation
                        _driver.SetMonitorOrientation(monitorDeviceName, dev.CurrentOrientation);
                        
                        // Raise event
                        OrientationChanged?.Invoke(this, (dev, dev.CurrentOrientation));
                        
                        _logAction($"Device {dev.MacAddress} orientation changed to {dev.CurrentOrientation}");
                    }
                };
                
                // Add to temp devices
                _tempDevices[port] = device;
                
                // Try to open the port
                if (device.Open())
                {
                    // Wait for identification
                    Thread.Sleep(DETECTION_TIMEOUT_MS);
                    
                    // If not identified after timeout, close and remove
                    if (!device.IsIdentified)
                    {
                        _tempDevices.TryRemove(port, out _);
                        device.Dispose();
                        _logAction($"No ESP32 device identified on {port}");
                    }
                }
                else
                {
                    // Failed to open, remove
                    _tempDevices.TryRemove(port, out _);
                    device.Dispose();
                    _logAction($"Failed to open {port}");
                }
            }
            catch (Exception ex)
            {
                _logAction($"Error checking port {port}: {ex.Message}");
            }
            finally
            {
                // Remove from ports being checked
                _portsBeingChecked.TryRemove(port, out _);
            }
        }
    }
}