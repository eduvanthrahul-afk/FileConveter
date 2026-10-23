using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OrbitConvert;

/// <summary>Watches for Shift being pressed while the left mouse button is held.</summary>
internal sealed class ShiftDragHook : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int VkShift = 0x10;
    private const int VkLButton = 0x01;

    private readonly LowLevelKeyboardProc _callback;
    private readonly Action _onShiftDrag;
    private readonly IntPtr _hook;
    private bool _triggeredForPress;

    public ShiftDragHook(Action onShiftDrag)
    {
        _onShiftDrag = onShiftDrag;
        _callback = HookCallback;
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hook = SetWindowsHookEx(WhKeyboardLl, _callback, GetModuleHandle(module.ModuleName), 0);
    }

    private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && (wParam == (IntPtr)WmKeyDown || wParam == (IntPtr)WmSysKeyDown))
        {
            var virtualKey = Marshal.ReadInt32(lParam);
            if (virtualKey == VkShift && !_triggeredForPress &&
                (GetAsyncKeyState(VkLButton) & 0x8000) != 0)
            {
                _triggeredForPress = true;
                _onShiftDrag();
            }
        }
        else if (code >= 0 && (wParam == (IntPtr)WmKeyUp || wParam == (IntPtr)WmSysKeyUp) &&
                 Marshal.ReadInt32(lParam) == VkShift)
        {
            _triggeredForPress = false;
        }

        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
            UnhookWindowsHookEx(_hook);
    }

    private delegate IntPtr LowLevelKeyboardProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc callback, IntPtr module, uint threadId);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
