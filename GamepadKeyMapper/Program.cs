using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SharpDX.DirectInput;
using Newtonsoft.Json;
using System.IO;
using System.Linq;

class Program
{
    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    const int KEYEVENTF_KEYDOWN = 0x0000;
    const int KEYEVENTF_KEYUP = 0x0002;

    class ButtonMapping
    {
        public string _comment { get; set; } = string.Empty;
        public int GamepadButton { get; set; } = 0;
        public int[] KeyboardKeys { get; set; } = [];
    }
    class MappingConfig
    {
        public List<ButtonMapping> ButtonMappings { get; set; } = [];
    }

    static void Main(string[] args)
    {
        var directInput = new DirectInput();
        var joystick = directInput.GetDevices(DeviceType.Gamepad, DeviceEnumerationFlags.AllDevices).FirstOrDefault();

        if (joystick == null)
        {
            Console.WriteLine("No gamepad found.");
            return;
        }

        var device = new Joystick(directInput, joystick.InstanceGuid);
        device.Acquire();

        // Load button mappings from JSON
        var mappingConfig = JsonConvert.DeserializeObject<MappingConfig>(File.ReadAllText("button_mappings.json"));
        var buttonMappings = mappingConfig?.ButtonMappings ?? new List<ButtonMapping>();

        Console.WriteLine("Gamepad connected. Press Ctrl+C to exit.");
        Console.WriteLine("Loaded button mappings:");
        foreach (var mapping in buttonMappings)
        {
            Console.WriteLine($"{mapping._comment} - Gamepad button: {mapping.GamepadButton}, Keyboard keys: {string.Join(", ", mapping.KeyboardKeys)}");
        }

        while (true)
        {
            device.Poll();
            var state = device.GetCurrentState();

            for (int i = 0; i < state.Buttons.Length; i++)
            {
                var mapping = buttonMappings.FirstOrDefault(m => m.GamepadButton == i);
                if (mapping != null)
                {
                    if (state.Buttons[i])
                    {
                        foreach (var key in mapping.KeyboardKeys)
                        {
                            keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                        }
                    }
                    else
                    {
                        foreach (var key in mapping.KeyboardKeys)
                        {
                            keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                    }
                }
            }

            System.Threading.Thread.Sleep(10);
        }
    }
}