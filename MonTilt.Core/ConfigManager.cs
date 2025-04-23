using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace MonTilt.Core
{
    /// <summary>
    /// Manages configuration and device-to-monitor mappings
    /// </summary>
    public class ConfigManager
    {
        // Constants
        private const string DEFAULT_CONFIG_FILE = "montilt_config.json";
        
        // Properties
        public string ConfigFilePath { get; private set; }
        
        // Configuration data
        private ConfigData _config;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public ConfigManager(string configFilePath = null)
        {
            ConfigFilePath = configFilePath ?? DEFAULT_CONFIG_FILE;
            _config = new ConfigData();
        }
        
        /// <summary>
        /// Load configuration from file
        /// </summary>
        public bool LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var loadedConfig = JsonSerializer.Deserialize<ConfigData>(json);
                    
                    if (loadedConfig != null)
                    {
                        _config = loadedConfig;
                        
                        // Initialize collections if they don't exist
                        _config.DeviceToMonitorMap ??= new Dictionary<string, string>();
                        _config.DeviceToPortMap ??= new Dictionary<string, string>();
                        
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                // Use default config if loading fails
                _config = new ConfigData
                {
                    DeviceToMonitorMap = new Dictionary<string, string>(),
                    DeviceToPortMap = new Dictionary<string, string>()
                };
            }
            
            return false;
        }
        
        /// <summary>
        /// Save configuration to file
        /// </summary>
        public bool SaveConfig()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };
                
                string json = JsonSerializer.Serialize(_config, options);
                File.WriteAllText(ConfigFilePath, json);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Get device-to-monitor mappings
        /// </summary>
        public Dictionary<string, string> GetDeviceToMonitorMap()
        {
            return new Dictionary<string, string>(_config.DeviceToMonitorMap);
        }
        
        /// <summary>
        /// Set device-to-monitor mappings
        /// </summary>
        public void SetDeviceToMonitorMap(Dictionary<string, string> mappings)
        {
            _config.DeviceToMonitorMap = new Dictionary<string, string>(mappings);
        }
        
        /// <summary>
        /// Add or update a device-to-monitor mapping
        /// </summary>
        public void AddOrUpdateMapping(string deviceMac, string monitorDeviceName)
        {
            _config.DeviceToMonitorMap[deviceMac] = monitorDeviceName;
        }
        
        /// <summary>
        /// Remove a device-to-monitor mapping
        /// </summary>
        public bool RemoveMapping(string deviceMac)
        {
            return _config.DeviceToMonitorMap.Remove(deviceMac);
        }
        
        /// <summary>
        /// Update device COM port
        /// </summary>
        public void UpdateDevicePort(string deviceMac, string comPort)
        {
            _config.DeviceToPortMap[deviceMac] = comPort;
        }
        
        /// <summary>
        /// Get device COM port
        /// </summary>
        public string GetDevicePort(string deviceMac)
        {
            return _config.DeviceToPortMap.TryGetValue(deviceMac, out string port) ? port : null;
        }
        
        /// <summary>
        /// Configuration data class
        /// </summary>
        private class ConfigData
        {
            public Dictionary<string, string> DeviceToMonitorMap { get; set; } = new Dictionary<string, string>();
            public Dictionary<string, string> DeviceToPortMap { get; set; } = new Dictionary<string, string>();
        }
    }
}