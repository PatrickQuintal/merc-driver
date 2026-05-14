using System.Runtime.InteropServices;

namespace Merc.Mapper.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct RawInputDevice
{
    public ushort UsagePage;
    public ushort Usage;
    public uint Flags;
    public IntPtr TargetWindow;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RawInputHeader
{
    public uint Type;
    public uint Size;
    public IntPtr Device;
    public IntPtr WParam;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RawKeyboard
{
    public ushort MakeCode;
    public ushort Flags;
    public ushort Reserved;
    public ushort VKey;
    public uint Message;
    public uint ExtraInformation;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RawInput
{
    public RawInputHeader Header;
    public RawKeyboard Keyboard;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WindowClassEx
{
    public uint Size;
    public uint Style;
    public RawInputApi.WindowProcedure WindowProcedure;
    public int ClassExtraBytes;
    public int WindowExtraBytes;
    public IntPtr Instance;
    public IntPtr Icon;
    public IntPtr Cursor;
    public IntPtr Background;
    public string? MenuName;
    public string ClassName;
    public IntPtr IconSmall;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Message
{
    public IntPtr Hwnd;
    public uint Value;
    public IntPtr WParam;
    public IntPtr LParam;
    public uint Time;
    public int PointX;
    public int PointY;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardLowLevelHook
{
    public uint VirtualKeyCode;
    public uint ScanCode;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Explicit)]
internal struct Input
{
    [FieldOffset(0)]
    public uint Type;

    [FieldOffset(8)]
    public KeyboardInput Keyboard;

    [FieldOffset(8)]
    public MouseInput Mouse;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KeyboardInput
{
    public ushort VirtualKey;
    public ushort Scan;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MouseInput
{
    public int X;
    public int Y;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public IntPtr ExtraInfo;
}
