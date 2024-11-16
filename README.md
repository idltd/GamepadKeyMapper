# Gamepad Mapper

A C# application that allows you to map gamepad/controller inputs to keyboard events. This tool is useful for games or applications that don't natively support controller input or when you want to customize your controller mapping.

## Features

- Map gamepad buttons to keyboard keys
- Map analog axes to keyboard keys with customizable thresholds
- Support for multiple controller profiles
- Verbose mode for debugging
- Support for modifier keys (Shift, Ctrl, Alt, Windows)
- Profile listing functionality
- Compatible with various types of game controllers (not just Xbox-style gamepads)

## Prerequisites

- .NET Core 6.0 or later
- Windows operating system
- DirectX runtime
- SharpDX.DirectInput NuGet package
- System.CommandLine NuGet package
- Newtonsoft.Json NuGet package

## Installation

1. Clone the repository or download the source code
2. Install the required NuGet packages:
   ```bash
   dotnet restore
   ```
3. Build the project:
   ```bash
   dotnet build
   ```

## Usage

### Basic Usage

Run the program with default settings:
```bash
dotnet run
```

### Command Line Options

- `-c, --config <path>`: Specify the path to the configuration file (default: button_mappings.json)
- `-p, --profile <name>`: Specify the profile to use (default: default)
- `-v, --verbose`: Enable verbose output for debugging
- `-l, --list`: List available profiles and exit

### Examples

List available profiles:
```bash
dotnet run --list
```

Use a specific profile:
```bash
dotnet run --profile xbox_profile
```

Use verbose mode with a custom config file:
```bash
dotnet run --config my_mappings.json --verbose
```

## Configuration File Format

The configuration file should be in JSON format with the following structure:

```json
{
  "Profiles": {
    "default": {
      "ButtonMappings": [
        {
          "GamepadButton": 0,
          "KeyboardKeys": [32]  // Space bar
        }
      ],
      "AxisMappings": [
        {
          "Axis": 0,
          "Direction": "positive",
          "KeyboardKeys": [39]  // Right arrow
        }
      ]
    }
  }
}
```

### Button Mapping

- `GamepadButton`: The index of the gamepad button (0-based)
- `KeyboardKeys`: Array of virtual key codes to trigger

### Axis Mapping

- `Axis`: The axis index (0 = X, 1 = Y, 2 = Z, 3 = RotationX, etc.)
- `Direction`: "positive" or "negative"
- `KeyboardKeys`: Array of virtual key codes to trigger

## Virtual Key Codes

Common virtual key codes for reference:
- Arrow Keys: Left (37), Up (38), Right (39), Down (40)
- WASD: W (87), A (65), S (83), D (68)
- Modifier Keys: Shift (16), Ctrl (17), Alt (18)
- Space: 32
- Enter: 13

For a complete list of virtual key codes, refer to the [Microsoft Virtual-Key Codes documentation](https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes).

## Troubleshooting

### Controller Not Detected

If your controller isn't being detected:
1. Ensure it's properly connected and recognized in Windows
2. Try unplugging and reconnecting the controller
3. Run the GamepadLister tool (included in the repository) to verify DirectInput can see your controller
4. Check if your controller requires specific drivers

### Input Lag or Missed Inputs

1. Adjust the `AxisThreshold` value in the code (default is 1)
2. Modify the `AxisResolutionDivisor` value (default is 10000)
3. Reduce the delay in the main loop (default is 5ms)

## Contributing

Contributions are welcome! Please feel free to submit pull requests or create issues for bugs and feature requests.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- SharpDX team for the DirectInput wrapper
- System.CommandLine team for the command-line parsing functionality
