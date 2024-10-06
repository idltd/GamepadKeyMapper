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

    private GamepadState gamepadState = new GamepadState();

    class ButtonMapping
    {
        public string _comment { get; set; } = string.Empty;
        public int GamepadButton { get; set; } = 0;
        public int[] KeyboardKeys { get; set; } = [];
    }

    class Profile
    {
        public List<ButtonMapping> ButtonMappings { get; set; } = [];
    }

    class MappingConfig
    {
        public Dictionary<string, Profile> Profiles { get; set; } = [];
    }

    class GamepadState
    {
        private Dictionary<int, bool> buttonStates = new Dictionary<int, bool>();

        public bool UpdateButtonState(int buttonIndex, bool currentState)
        {
            if (!buttonStates.ContainsKey(buttonIndex))
            {
                buttonStates[buttonIndex] = false;
            }

            if (currentState && !buttonStates[buttonIndex])
            {
                // Button is pressed for the first time
                buttonStates[buttonIndex] = true;
                return true;
            }
            else if (!currentState && buttonStates[buttonIndex])
            {
                // Button is released
                buttonStates[buttonIndex] = false;
            }

            // Ignore repeated presses
            return false;
        }
    }

    static async Task<int> Main(string[] args)
    {
        var program = new Program();
        return await program.RunAsync(args);
    }

    async Task<int> RunAsync(string[] args)
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

            await ExecuteAsync(config!, profile!, verbose, list);
        });

        return await rootCommand.InvokeAsync(args);
    }

    async Task ExecuteAsync(FileInfo configFile, string profileName, bool verbose, bool list)
    {
        if (configFile == null)
        {
            Console.WriteLine("Error: Configuration file not specified.");
            return;
        }

        if (!configFile.Exists)
        {
            Console.WriteLine($"Error: Configuration file '{configFile.FullName}' not found.");
            return;
        }

        if (string.IsNullOrEmpty(profileName))
        {
            Console.WriteLine("Error: Profile name not specified.");
            return;
        }

        MappingConfig? mappingConfig;
        try
        {
            string jsonContent = await File.ReadAllTextAsync(configFile.FullName);
            mappingConfig = JsonConvert.DeserializeObject<MappingConfig>(jsonContent);
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

        var buttonMappings = mappingConfig.Profiles[profileName].ButtonMappings;

        Console.WriteLine($"Using profile: {profileName}");
        Console.WriteLine("Loaded button mappings:");
        foreach (var mapping in buttonMappings)
        {
            Console.WriteLine($"{mapping._comment} - Gamepad button: {mapping.GamepadButton}, Keyboard keys: {string.Join(", ", mapping.KeyboardKeys)}");
        }

        if (verbose)
        {
            Console.WriteLine("Verbose mode enabled. Reporting all commands and keystrokes.");
        }

        var directInput = new DirectInput();
        var joystick = directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices).FirstOrDefault();

        if (joystick == null)
        {
            Console.WriteLine("No gamepad found.");
            return;
        }

        var device = new Joystick(directInput, joystick.InstanceGuid);
        device.Acquire();

        Console.WriteLine("Gamepad connected. Press Ctrl+C to exit.");

        while (true)
        {
            device.Poll();
            var state = device.GetCurrentState();

            for (int i = 0; i < state.Buttons.Length; i++)
            {
                var mapping = buttonMappings.FirstOrDefault(m => m.GamepadButton == i);
                if (mapping != null)
                {
                    bool isNewPress = this.gamepadState.UpdateButtonState(i, state.Buttons[i]);
                    if (isNewPress)
                    {
                        if (verbose)
                        {
                            Console.WriteLine($"Gamepad button {i} pressed. Sending keys: {string.Join(", ", mapping.KeyboardKeys)}");
                        }
                        foreach (var key in mapping.KeyboardKeys)
                        {
                            keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                    }
                }

                await Task.Delay(10);
            }
        }
    }
}