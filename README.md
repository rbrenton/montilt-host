# MonTilt Host

MonTilt Host is a Windows application that automatically rotates monitor orientation based on physical sensor data from ESP32 devices. This is the main component of the MonTilt system, which consists of this host application and the [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) firmware.

## Features

- Automatically detects and connects to ESP32 devices running MonTilt Sensor firmware
- Maps specific ESP32 sensors to specific monitors by MAC address
- Rotates monitor displays based on real-time orientation data from sensors
- Supports landscape (0°), portrait (90°), inverted landscape (180°), and inverted portrait (270°) orientations
- Persists configuration between sessions
- Simple command-line interface for management

## Requirements

- Windows 10 or later
- .NET 5.0 or later
- One or more ESP32 devices running [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) firmware

## Usage

1. Connect ESP32 devices with MonTilt Sensor firmware to USB ports
2. Launch the application
3. Use the following commands:
   - `map [mac address] [monitor number]` - Map a device to a specific monitor
   - `list` - List current device mappings
   - `scan` - Rescan for new ESP32 devices
   - `save` - Save current configuration
   - `exit` - Exit the application

## How It Works

The application:
1. Scans for ESP32 devices connected via USB
2. Identifies them by MAC address
3. Allows mapping each device to a specific monitor
4. Receives orientation data from the sensors
5. Rotates the corresponding monitors accordingly

## Building

Open the solution in Visual Studio and build.

## Related Projects

- [MonTilt Sensor](https://github.com/rbrenton/montilt-sensor) - ESP32 firmware for orientation detection