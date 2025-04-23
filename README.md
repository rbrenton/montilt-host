# MonTilt

MonTilt is a Windows application that automatically rotates monitor orientation based on physical sensor data from ESP32 devices running the [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) firmware.

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
```
dotnet build
```

## How It Works

1. The application scans for ESP32 devices connected via USB
2. It identifies them by MAC address and maintains device-to-monitor mappings
3. When orientation changes are detected from the sensors, it rotates the corresponding monitors using the Windows API
4. Configuration is persisted between sessions

## Related Projects

- [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) - ESP32 firmware for orientation detection