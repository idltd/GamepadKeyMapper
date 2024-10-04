using System;
using System.Runtime.InteropServices;
using SharpDX.DirectInput;

class Program
{
    [DllImport("user32.dll")]
    static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    const int KEYEVENTF_KEYDOWN = 0x0000;
    const int KEYEVENTF_KEYUP = 0x0002;

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

        Console.WriteLine("Gamepad connected. Press Ctrl+C to exit.");

        while (true)
        {
            device.Poll();
            var state = device.GetCurrentState();

            // Map A button to Space key
            if (state.Buttons[0])
                keybd_event(0x20, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
            else
                keybd_event(0x20, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);

            // Add more button mappings here

            System.Threading.Thread.Sleep(10);
        }
    }
}