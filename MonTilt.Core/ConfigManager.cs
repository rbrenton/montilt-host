using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonTilt.Core
{
    /// <summary>
    /// Manages configuration settings for the MonTilt application
    /// </summary>
    public class ConfigManager
    {
        // Configuration file path
        private const string CONFIG_FILE = "montilt_config.json";

        /// <summary>
        /// Dictionary mapping device MAC addresses to monitor indices
        /// </summary>
        public Dictionary<string, int> DeviceMonitorMap { get; private set; } = new Dictionary<string, int>();

        /// <summary>
        /// Dictionary mapping device MAC addresses to COM ports
        /// </summary>
        public Dictionary<string, string> DevicePortMap { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Event raised when configuration is updated
        /// </summary>
        public event EventHandler? ConfigurationUpdated;

        /// <summary>
        /// Constructor for ConfigManager
        /// </summary>
        public ConfigManager()
        {
            // Initialize empty maps
            DeviceMonitorMap = new Dictionary<string, int>();
            DevicePortMap = new Dictionary<string, string>();
        }

        /// <summary>
        /// Loads configuration from file or creates default configuration if file doesn't exist
        /// </summary>
        /// <returns>True if configuration was loaded successfully, false otherwise</returns>
        public bool LoadConfiguration()
        {
            try
            {
                if (File.Exists(CONFIG_FILE))
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    MonTiltConfig? config = JsonSerializer.Deserialize<MonTiltConfig>(json);

                    if (config != null)
                    {
                        // Initialize from loaded config
                        DeviceMonitorMap = config.DeviceMonitorMap ?? new Dictionary<string, int>();
                        DevicePortMap = config.DevicePortMap ?? new Dictionary<string, string>();

                        Console.WriteLine("Configuration loaded successfully.");
                        return true;
                    }
                }

                // Create default configuration if file doesn't exist or deserialization failed
                Console.WriteLine("Using default configuration.");
                CreateDefaultConfiguration();
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                CreateDefaultConfiguration();
                return false;
            }
        }

        /// <summary>
        /// Saves current configuration to file
        /// </summary>
        /// <returns>True if configuration was saved successfully, false otherwise</returns>
        public bool SaveConfiguration()
        {
            try
            {
                MonTiltConfig config = new MonTiltConfig
                {
                    DeviceMonitorMap = DeviceMonitorMap,
                    DevicePortMap = DevicePortMap
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                File.WriteAllText(CONFIG_FILE, json);
                Console.WriteLine("Configuration saved successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Maps a device to a monitor
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <param name="monitorIndex">The monitor index</param>
        public void MapDeviceToMonitor(string macAddress, int monitorIndex)
        {
            DeviceMonitorMap[macAddress] = monitorIndex;
            ConfigurationUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Unmaps a device from its monitor
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <returns>True if the device was unmapped, false if it wasn't mapped</returns>
        public bool UnmapDevice(string macAddress)
        {
            bool removed = DeviceMonitorMap.Remove(macAddress);
            
            if (removed)
            {
                ConfigurationUpdated?.Invoke(this, EventArgs.Empty);
            }
            
            return removed;
        }

        /// <summary>
        /// Updates the COM port for a device
        /// </summary>
        /// <param name="macAddress">The MAC address of the device</param>
        /// <param name="comPort">The COM port</param>
        public void UpdateDevicePort(string macAddress, string comPort)
        {
            DevicePortMap[macAddress] = comPort;
            ConfigurationUpdated?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Creates default configuration
        /// </summary>
        private void CreateDefaultConfiguration()
        {
            DeviceMonitorMap = new Dictionary<string, int>();
            DevicePortMap = new Dictionary<string, string>();
        }

        /// <summary>
        /// Configuration class for serialization
        /// </summary>
        private class MonTiltConfig
        {
            [JsonPropertyName("deviceMonitorMap")]
            public Dictionary<string, int>? DeviceMonitorMap { get; set; }

            [JsonPropertyName("devicePortMap")]
            public Dictionary<string, string>? DevicePortMap { get; set; }
        }
    }
}