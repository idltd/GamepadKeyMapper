using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharpDX.DirectInput;

class Program
{
    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    const int KEYEVENTF_KEYDOWN = 0x0000;
    const int KEYEVENTF_KEYUP = 0x0002;
    private Dictionary<int, int> previousAxisValues = new Dictionary<int, int>();
    private static readonly HashSet<int> ModifierKeys = new HashSet<int>
    {
        0x10, // VK_SHIFT
        0x11, // VK_CONTROL
        0x12, // VK_ALT
        0x5B, // VK_LWIN (Left Windows key)
        0x5C  // VK_RWIN (Right Windows key)
    };

    private Dictionary<int, ButtonMapping> buttonMappings = new Dictionary<int, ButtonMapping>();
    private Dictionary<int, List<AxisMapping>> axisMappings = new Dictionary<int, List<AxisMapping>>();
    private HashSet<int> pressedButtons = new HashSet<int>();

    private const int AxisResolutionDivisor = 10000;
    private const int AxisThreshold = 1; // Adjusted for the new scale

    class ButtonMapping
    {
        public int GamepadButton { get; set; }
        public List<int> KeyboardKeys { get; set; } = new List<int>();
    }

    class AxisMapping
    {
        public int Axis { get; set; }
        public required string Direction { get; set; }
        public List<int> KeyboardKeys { get; set; } = new List<int>();
    }

    class Profile
    {
        public List<ButtonMapping> ButtonMappings { get; set; } = new List<ButtonMapping>();
        public List<AxisMapping> AxisMappings { get; set; } = new List<AxisMapping>();
    }

    class MappingConfig
    {
        public Dictionary<string, Profile> Profiles { get; set; } = new Dictionary<string, Profile>();
    }

    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand
        {
            Description = "Gamepad Key Mapper - Map gamepad inputs to keyboard events"
        };

        var configOption = new Option<FileInfo>(
            aliases: new[] { "--config", "-c" },
            description: "Path to the configuration file.",
            getDefaultValue: () => new FileInfo("button_mappings.json"))
        {
            IsRequired = false,
            Arity = ArgumentArity.ExactlyOne
        };
        configOption.AddValidator(result =>
        {
            var file = result.GetValueForOption(configOption);
            if (file != null && !file.Exists)
            {
                result.ErrorMessage = $"Configuration file not found: {file.FullName}";
            }
        });

        var profileOption = new Option<string>(
            aliases: new[] { "--profile", "-p" },
            description: "Profile name to use from the configuration.",
            getDefaultValue: () => "default")
        {
            IsRequired = false,
            Arity = ArgumentArity.ExactlyOne
        };

        var verboseOption = new Option<bool>(
            aliases: new[] { "--verbose", "-v" },
            description: "Enable verbose output.")
        {
            IsRequired = false,
            Arity = ArgumentArity.Zero
        };

        var listOption = new Option<bool>(
            aliases: new[] { "--list", "-l" },
            description: "List available profiles and exit.")
        {
            IsRequired = false,
            Arity = ArgumentArity.Zero
        };

        rootCommand.AddOption(configOption);
        rootCommand.AddOption(profileOption);
        rootCommand.AddOption(verboseOption);
        rootCommand.AddOption(listOption);

        rootCommand.SetHandler(async (InvocationContext context) =>
        {
            var config = context.ParseResult.GetValueForOption(configOption);
            var profile = context.ParseResult.GetValueForOption(profileOption);
            var verbose = context.ParseResult.GetValueForOption(verboseOption);
            var list = context.ParseResult.GetValueForOption(listOption);

            var program = new Program();
            await program.ExecuteAsync(config!, profile!, verbose, list);
        });

        return await rootCommand.InvokeAsync(args);
    }

    private void InitializeMappings(MappingConfig config, string profileName)
    {
        var profile = config.Profiles[profileName];

        foreach (var mapping in profile.ButtonMappings)
        {
            buttonMappings[mapping.GamepadButton] = new ButtonMapping
            {
                GamepadButton = mapping.GamepadButton,
                KeyboardKeys = mapping.KeyboardKeys
            };
        }

        foreach (var mapping in profile.AxisMappings)
        {
            if (!axisMappings.ContainsKey(mapping.Axis))
            {
                axisMappings[mapping.Axis] = new List<AxisMapping>();
            }
            axisMappings[mapping.Axis].Add(new AxisMapping
            {
                Axis = mapping.Axis,
                Direction = mapping.Direction,
                KeyboardKeys = mapping.KeyboardKeys
            });
        }
    }

    private void ProcessButtonPress(ButtonMapping mapping)
    {
        var currentModifiers = new HashSet<int>();

        foreach (var key in mapping.KeyboardKeys)
        {
            if (ModifierKeys.Contains(key))
            {
                currentModifiers.Add(key);
                keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            }
            else
            {
                keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

                foreach (var modifier in currentModifiers)
                {
                    keybd_event((byte)modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                }
                currentModifiers.Clear();
            }
        }

        foreach (var modifier in currentModifiers)
        {
            keybd_event((byte)modifier, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    private void ProcessAxis(int axisValue, int axisIndex, bool verbose)
    {
        int scaledValue = axisValue / AxisResolutionDivisor;

        if (!previousAxisValues.TryGetValue(axisIndex, out int previousValue) || previousValue != scaledValue)
        {
            previousAxisValues[axisIndex] = scaledValue;

            if (verbose)
            {
                Console.WriteLine($"Axis {axisIndex}: Raw Value = {axisValue}, Scaled Value = {scaledValue}");
            }

            if (!axisMappings.ContainsKey(axisIndex))
            {
                if (verbose)
                {
                    Console.WriteLine($"No mapping found for Axis {axisIndex}");
                }
                return;
            }

            foreach (var mapping in axisMappings[axisIndex])
            {
                int relativePosition = scaledValue - 3; // Assuming 16-bit range scaled down, center is now at 3
                bool exceededThreshold = (mapping.Direction == "positive" && relativePosition > AxisThreshold) ||
                                         (mapping.Direction == "negative" && relativePosition < -AxisThreshold);

                if (verbose)
                {
                    Console.WriteLine($"Axis {axisIndex}: Direction = {mapping.Direction}, Relative Position = {relativePosition}, Exceeded Threshold = {exceededThreshold}");
                }

                if (exceededThreshold)
                {
                    if (verbose)
                    {
                        Console.WriteLine($"Triggering keys for Axis {axisIndex}: {string.Join(", ", mapping.KeyboardKeys)}");
                    }

                    ProcessButtonPress(new ButtonMapping { KeyboardKeys = mapping.KeyboardKeys });
                }
            }
        }
    }
    async Task ExecuteAsync(FileInfo configFile, string profileName, bool verbose, bool list)
    {
        if (!configFile.Exists)
        {
            Console.WriteLine($"Error: Configuration file '{configFile.FullName}' not found.");
            return;
        }

        MappingConfig mappingConfig;
        try
        {
            string jsonContent = await File.ReadAllTextAsync(configFile.FullName);
            mappingConfig = JsonConvert.DeserializeObject<MappingConfig>(jsonContent) ?? new MappingConfig();
            if (mappingConfig == null)
            {
                Console.WriteLine("Error: Failed to deserialize configuration file.");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading configuration file: {ex.Message}");
            return;
        }

        if (mappingConfig.Profiles == null || mappingConfig.Profiles.Count == 0)
        {
            Console.WriteLine("Error: Configuration file is invalid or empty.");
            return;
        }

        if (list)
        {
            Console.WriteLine("Available profiles:");
            foreach (var profile in mappingConfig.Profiles.Keys)
            {
                Console.WriteLine($"- {profile}");
            }
            return;
        }

        if (!mappingConfig.Profiles.ContainsKey(profileName))
        {
            Console.WriteLine($"Profile '{profileName}' not found. Available profiles: {string.Join(", ", mappingConfig.Profiles.Keys)}");
            return;
        }

        InitializeMappings(mappingConfig, profileName);

        Console.WriteLine($"Using profile: {profileName}");
        Console.WriteLine("Loaded button mappings:");
        foreach (var mapping in buttonMappings.Values)
        {
            Console.WriteLine($"Gamepad button: {mapping.GamepadButton}, Keyboard keys: {string.Join(", ", mapping.KeyboardKeys)}");
        }

        if (verbose)
        {
            Console.WriteLine("Verbose mode enabled. Reporting all commands and keystrokes.");
        }

        var directInput = new DirectInput();
        // Changed from DeviceType.Gamepad to DeviceClass.GameControl
        var gameControllers = directInput.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AllDevices);

        if (!gameControllers.Any())
        {
            Console.WriteLine("No game controllers found.");
            return;
        }

        // Get the first available controller
        var controller = gameControllers.FirstOrDefault();
        if (controller == null)
        {
            Console.WriteLine("No game controller found.");
            return;
        }

        var device = new Joystick(directInput, controller.InstanceGuid);
        device.Acquire();

        Console.WriteLine($"Connected to: {controller.InstanceName}");
        Console.WriteLine("Press Ctrl+C to exit.");

        if (verbose)
        {
            var caps = device.Capabilities;
            Console.WriteLine($"Button Count: {caps.ButtonCount}");
            Console.WriteLine($"Axis Count: {caps.AxeCount}");
            Console.WriteLine($"POV Count: {caps.PovCount}");
        }

        while (true)
        {
            device.Poll();
            var state = device.GetCurrentState();


            if (verbose)
            {
                Console.WriteLine($"Buttons: {string.Join(", ", state.Buttons)}");
                Console.WriteLine($"X: {state.X}, Y: {state.Y}");
            }
            // Process buttons
            for (int i = 0; i < state.Buttons.Length; i++)
            {
                if (state.Buttons[i])
                {
                    if (!pressedButtons.Contains(i))
                    {
                        pressedButtons.Add(i);
                        if (buttonMappings.TryGetValue(i, out ButtonMapping? mapping) && mapping != null)
                        {
                            if (verbose)
                            {
                                Console.WriteLine($"Gamepad button {i} pressed. Sending keys: {string.Join(", ", mapping.KeyboardKeys)}");
                            }
                            ProcessButtonPress(mapping);
                        }
                        else if (verbose)
                        {
                            Console.WriteLine($"Gamepad button {i} pressed. No mapping found.");
                        }
                    }
                }
                else
                {
                    pressedButtons.Remove(i);
                }
            }

            // Process axes
            ProcessAxis(state.X, 0, verbose);
            ProcessAxis(state.Y, 1, verbose);
            ProcessAxis(state.Z, 2, verbose);
            ProcessAxis(state.RotationX, 3, verbose);
            ProcessAxis(state.RotationY, 4, verbose);
            ProcessAxis(state.RotationZ, 5, verbose);
            // Add more axes as needed, e.g., sliders
            for (int i = 0; i < state.Sliders.Length; i++)
            {
                ProcessAxis(state.Sliders[i], 6 + i, verbose);
            }
            await Task.Delay(5);
        }
    }
}