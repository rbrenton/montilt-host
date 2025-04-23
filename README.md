# MonTilt

MonTilt is a Windows application that automatically rotates monitor orientation based on physical sensor data from ESP32 devices running the [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) firmware.

## New Architecture

The application has been refactored into a more modular architecture with three main components:

1. **MonTilt.Driver**: Windows API interface for monitor orientation control
2. **MonTilt.Core**: Device management and configuration 
3. **MonTilt.CLI**: Command-line interface for configuration and monitoring

## Features

- **Monitor Control**: Rotate monitor displays using Windows API
- **Device Management**: Auto-detect and connect to ESP32 devices
- **Configuration**: Save and load device-to-monitor mappings
- **CLI Interface**: Simple command-line interface for management
- **Logging**: Detailed logging with multiple levels for debugging

## Requirements

- Windows 10 or later
- .NET 6.0 or later
- One or more ESP32 devices running [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) firmware

## CLI Commands
```
MonTilt Commands:
help                     - Show this help text
list monitors            - List available monitors
list devices             - List connected devices
list mappings            - List device-to-monitor mappings
map <mac> <monitor>      - Map a device to a monitor
unmap <mac>              - Remove a device mapping
scan                     - Scan for new devices
save                     - Save current configuration
log <level>              - Set log level (debug, info, warning, error)
exit                     - Exit the application
```

## Building

1. Open the solution in Visual Studio 2022 or later
2. Ensure you have the .NET 6.0 SDK installed
3. Build the solution

## How It Works

1. The application scans for ESP32 devices connected via USB
2. It identifies them by MAC address and maintains device-to-monitor mappings
3. When orientation changes are detected from the sensors, it rotates the corresponding monitors using the Windows API
4. Configuration is persisted between sessions

## Related Projects

- [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) - ESP32 firmware for orientation detection