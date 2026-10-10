using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MDPaste
{
    internal sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private int registeredId;
        internal bool IsRegistered => registeredId != 0;
        internal event EventHandler? Pressed;

        internal HotkeyWindow()
        {
            CreateHandle(new CreateParams { Caption = "MDPaste.Hotkey", Parent = new IntPtr(-3) });
        }

        internal void Register(Shortcut shortcut)
        {
            shortcut.Validate();
            int nextId = registeredId == 1 ? 2 : 1;
            if (!Native.RegisterHotKey(Handle, nextId, shortcut.Modifiers, (uint)shortcut.Key))
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Could not register " + shortcut + ". Another app or Windows may already use it. Choose another shortcut in Settings.");
            if (registeredId != 0)
                Native.UnregisterHotKey(Handle, registeredId);
            registeredId = nextId;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == registeredId)
                Pressed?.Invoke(this, EventArgs.Empty);
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            if (registeredId != 0)
                Native.UnregisterHotKey(Handle, registeredId);
            DestroyHandle();
        }
    }

    internal static class Native
    {
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            internal uint Type;
            internal INPUTUNION Data;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct INPUTUNION
        {
            [FieldOffset(0)] internal KEYBDINPUT Keyboard;
            [FieldOffset(0)] internal MOUSEINPUT Mouse;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            internal ushort VirtualKey;
            internal ushort Scan;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            internal int X;
            internal int Y;
            internal uint MouseData;
            internal uint Flags;
            internal uint Time;
            internal UIntPtr ExtraInfo;
        }

        internal static bool ShortcutReleased(Keys key) =>
            !Down(Keys.ControlKey) && !Down(Keys.ShiftKey) && !Down(Keys.Menu) &&
            !Down(Keys.LWin) && !Down(Keys.RWin) && !Down(key);

        private static bool Down(Keys key) => (GetAsyncKeyState((int)key) & 0x8000) != 0;

        internal static void Paste()
        {
            var inputs = new[] { Key(Keys.ControlKey, false), Key(Keys.V, false),
                                 Key(Keys.V, true), Key(Keys.ControlKey, true) };
            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
            if (sent != inputs.Length)
            {
                int error = Marshal.GetLastWin32Error();
                // Release any keys from a partially injected sequence.
                SendInput(2, new[] { Key(Keys.V, true), Key(Keys.ControlKey, true) }, Marshal.SizeOf(typeof(INPUT)));
                throw new Win32Exception(error,
                    "Windows blocked automatic paste. Clipboard conversion succeeded; paste manually with Ctrl+V. " +
                    "Apps running as administrator cannot receive keystrokes from this non-elevated app.");
            }
        }

        private static INPUT Key(Keys key, bool up) => new INPUT
        {
            Type = 1,
            Data = new INPUTUNION { Keyboard = new KEYBDINPUT { VirtualKey = (ushort)key, Flags = up ? 2u : 0u } }
        };
    }
}
