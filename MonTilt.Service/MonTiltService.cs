// MonTilt.Service/MonTiltService.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MonTilt.Core;
using MonTilt.Driver;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace MonTilt.Service
{
    public class MonTiltService : BackgroundService
    {
        private readonly ILogger<MonTiltService> _logger;
        private readonly DeviceManager _deviceManager;
        private readonly ConfigManager _configManager;
        private readonly MonitorOrientationDriver _monitorDriver;

        public MonTiltService(
            ILogger<MonTiltService> logger,
            DeviceManager deviceManager,
            ConfigManager configManager,
            MonitorOrientationDriver monitorDriver)
        {
            _logger = logger;
            _deviceManager = deviceManager;
            _configManager = configManager;
            _monitorDriver = monitorDriver;

            // Set up event handlers
            _deviceManager.DeviceDiscovered += OnDeviceDiscovered;
            _deviceManager.DeviceConnected += OnDeviceConnected;
            _deviceManager.DeviceDisconnected += OnDeviceDisconnected;
            _deviceManager.OrientationChanged += OnOrientationChanged;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("MonTilt Service starting");

                // Initialize monitor driver
                _monitorDriver.Initialize();

                // Load configuration
                _configManager.LoadConfiguration();

                // Start device discovery
                _deviceManager.StartDeviceDiscovery();

                _logger.LogInformation("MonTilt Service started successfully");

                // Keep the service running
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    
                    // Periodically save configuration (every hour)
                    _configManager.SaveConfiguration();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in MonTilt Service");
            }
            finally
            {
                // Stop device discovery and save configuration
                _deviceManager.StopDeviceDiscovery();
                _configManager.SaveConfiguration();
                _logger.LogInformation("MonTilt Service stopped");
            }
        }

        #region Event Handlers

        private void OnDeviceDiscovered(object? sender, string mac)
        {
            _logger.LogInformation("Device discovered: {Mac}", mac);
        }

        private void OnDeviceConnected(object? sender, ESP32Device device)
        {
            _logger.LogInformation("Device connected: {Mac} on {Port}", device.MacAddress, device.ComPort);
        }

        private void OnDeviceDisconnected(object? sender, string mac)
        {
            _logger.LogInformation("Device disconnected: {Mac}", mac);
        }

        private void OnOrientationChanged(object? sender, OrientationChangedEventArgs args)
        {
            var orientationName = args.Orientation switch
            {
                0 => "Landscape (0°)",
                1 => "Portrait (90°)",
                2 => "Landscape flipped (180°)",
                3 => "Portrait flipped (270°)",
                _ => $"Unknown ({args.Orientation}°)"
            };
            
            _logger.LogInformation("Monitor {MonitorIndex} changed to {Orientation}", args.MonitorIndex + 1, orientationName);
        }

        #endregion
    }
}