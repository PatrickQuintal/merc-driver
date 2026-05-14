using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Merc.Mapper.Interop;

namespace Merc.Mapper;

public sealed class MercInputMapper : IDisposable
{
    private static readonly IReadOnlyDictionary<ushort, ushort> SourceToTarget = new Dictionary<ushort, ushort>
    {
        [VirtualKeys.BrowserBack] = VirtualKeys.A,
        [VirtualKeys.BrowserForward] = VirtualKeys.D,
        [VirtualKeys.BrowserRefresh] = VirtualKeys.Q,
        [VirtualKeys.BrowserStop] = VirtualKeys.S,
        [VirtualKeys.BrowserSearch] = VirtualKeys.E,
        [VirtualKeys.BrowserFavorites] = VirtualKeys.R,
        [VirtualKeys.BrowserHome] = VirtualKeys.W,
        [VirtualKeys.LaunchMediaSelect] = VirtualKeys.Z,
        [VirtualKeys.LaunchApp2] = VirtualKeys.Tab,
        [VirtualKeys.Gamepad2T] = VirtualKeys.T,
        [VirtualKeys.Gamepad3G] = VirtualKeys.G,
        [VirtualKeys.Gamepad4V] = VirtualKeys.V,
        [VirtualKeys.Gamepad5B] = VirtualKeys.B,
        [VirtualKeys.Gamepad6C] = VirtualKeys.C,
        [VirtualKeys.Multiply] = VirtualKeys.Space,
        [VirtualKeys.Divide] = VirtualKeys.LeftShift
    };

    private static readonly IReadOnlyDictionary<KeyboardSourceKey, ushort> ScanSourceToTarget = new Dictionary<KeyboardSourceKey, ushort>
    {
        [new(VirtualKeys.Home, 0x47, false)] = VirtualKeys.Key7,
        [new(VirtualKeys.Up, 0x48, false)] = VirtualKeys.Key8,
        [new(VirtualKeys.PageUp, 0x49, false)] = VirtualKeys.Key9,
        [new(VirtualKeys.Insert, 0x52, false)] = VirtualKeys.Key0,
        [new(VirtualKeys.Add, 0x4E, false)] = VirtualKeys.OemPlus,
        [new(VirtualKeys.End, 0x4F, false)] = VirtualKeys.Key1,
        [new(VirtualKeys.Down, 0x50, false)] = VirtualKeys.Key2,
        [new(VirtualKeys.PageDown, 0x51, false)] = VirtualKeys.Key3,
        [new(VirtualKeys.Left, 0x4B, false)] = VirtualKeys.Key4,
        [new(VirtualKeys.Clear, 0x4C, false)] = VirtualKeys.Key5,
        [new(VirtualKeys.Right, 0x4D, false)] = VirtualKeys.Key6,
        [new(VirtualKeys.Clear, 0x59, false)] = VirtualKeys.F,
        [new(VirtualKeys.Delete, 0x53, false)] = VirtualKeys.LeftControl,
        [new(VirtualKeys.Decimal, 0x53, false)] = VirtualKeys.LeftControl
    };

    private static readonly ConsumerBitMapping[] ConsumerBitMappings =
    {
        new(1, 0x80, VirtualKeys.Z),
        new(2, 0x02, VirtualKeys.Tab),
        new(2, 0x08, VirtualKeys.E),
        new(2, 0x10, VirtualKeys.W),
        new(2, 0x20, VirtualKeys.A),
        new(2, 0x40, VirtualKeys.D),
        new(2, 0x80, VirtualKeys.S),
        new(3, 0x01, VirtualKeys.Q),
        new(3, 0x02, VirtualKeys.R)
    };

    private static readonly IReadOnlySet<ushort> SuppressedSourceKeys = new HashSet<ushort>
    {
        VirtualKeys.BrowserBack,
        VirtualKeys.BrowserForward,
        VirtualKeys.BrowserRefresh,
        VirtualKeys.BrowserStop,
        VirtualKeys.BrowserSearch,
        VirtualKeys.BrowserFavorites,
        VirtualKeys.BrowserHome,
        VirtualKeys.VolumeMute,
        VirtualKeys.VolumeDown,
        VirtualKeys.VolumeUp,
        VirtualKeys.MediaNextTrack,
        VirtualKeys.MediaPreviousTrack,
        VirtualKeys.MediaStop,
        VirtualKeys.MediaPlayPause,
        VirtualKeys.LaunchMail,
        VirtualKeys.LaunchMediaSelect,
        VirtualKeys.LaunchApp1,
        VirtualKeys.LaunchApp2,
        VirtualKeys.Gamepad2T,
        VirtualKeys.Gamepad3G,
        VirtualKeys.Gamepad4V,
        VirtualKeys.Gamepad5B,
        VirtualKeys.Gamepad6C,
        VirtualKeys.Multiply,
        VirtualKeys.Divide
    };

    private static readonly IReadOnlyDictionary<ushort, ushort> TargetScanCodes = new Dictionary<ushort, ushort>
    {
        [VirtualKeys.Tab] = 0x0F,
        [VirtualKeys.LeftControl] = 0x1D,
        [VirtualKeys.LeftShift] = 0x2A,
        [VirtualKeys.Space] = 0x39,
        [VirtualKeys.Key1] = 0x02,
        [VirtualKeys.Key2] = 0x03,
        [VirtualKeys.Key3] = 0x04,
        [VirtualKeys.Key4] = 0x05,
        [VirtualKeys.Key5] = 0x06,
        [VirtualKeys.Key6] = 0x07,
        [VirtualKeys.Key7] = 0x08,
        [VirtualKeys.Key8] = 0x09,
        [VirtualKeys.Key9] = 0x0A,
        [VirtualKeys.Key0] = 0x0B,
        [VirtualKeys.OemPlus] = 0x0D,
        [VirtualKeys.A] = 0x1E,
        [VirtualKeys.B] = 0x30,
        [VirtualKeys.C] = 0x2E,
        [VirtualKeys.D] = 0x20,
        [VirtualKeys.E] = 0x12,
        [VirtualKeys.F] = 0x21,
        [VirtualKeys.G] = 0x22,
        [VirtualKeys.Q] = 0x10,
        [VirtualKeys.R] = 0x13,
        [VirtualKeys.S] = 0x1F,
        [VirtualKeys.T] = 0x14,
        [VirtualKeys.V] = 0x2F,
        [VirtualKeys.W] = 0x11,
        [VirtualKeys.Z] = 0x2C
    };

    private readonly Action<string> _log;
    private readonly MapperOptions _options;
    private readonly ConcurrentDictionary<IntPtr, string> _deviceNames = new();
    private readonly MappedKeyState _keyState = new();
    private readonly object _stateLock = new();
    private readonly RawInputApi.WindowProcedure _windowProcedure;
    private readonly RawInputApi.LowLevelKeyboardProcedure _hookProcedure;
    private readonly ManualResetEventSlim _started = new();
    private Thread? _thread;
    private IntPtr _windowHandle;
    private IntPtr _hookHandle;
    private ShellAppCommandSuppressor? _shellSuppressor;
    private Timer? _repeatTimer;
    private Exception? _startupException;
    private string? _stopReason;
    private bool _disposed;
    private bool _startedDisposed;

    public MercInputMapper(Action<string> log, MapperOptions options)
    {
        _log = log;
        _options = options;
        _windowProcedure = WindowProcedure;
        _hookProcedure = HookProcedure;
    }

    public event Action<string?>? Stopped;

    public void Start(CancellationToken cancellationToken)
    {
        _thread = new Thread(MessageLoop)
        {
            IsBackground = true,
            Name = "Merc Mapper Raw Input"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        _started.Wait(cancellationToken);
        if (_startupException is not null)
        {
            throw new InvalidOperationException($"Mapper listener failed: {_startupException.Message}", _startupException);
        }

        cancellationToken.Register(() =>
        {
            if (_windowHandle != IntPtr.Zero)
            {
                RawInputApi.PostMessage(_windowHandle, RawInputApi.WmClose, IntPtr.Zero, IntPtr.Zero);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ReleaseAllMappedKeys();
        _repeatTimer?.Dispose();
        _repeatTimer = null;

        if (_hookHandle != IntPtr.Zero)
        {
            RawInputApi.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
        }

        if (_windowHandle != IntPtr.Zero)
        {
            RawInputApi.PostMessage(_windowHandle, RawInputApi.WmClose, IntPtr.Zero, IntPtr.Zero);
        }

        _thread?.Join(TimeSpan.FromSeconds(2));
        _log("Merc Mapper stopped.");
        DisposeStartedEvent();
    }

    public bool IsRunning => !_disposed && _thread?.IsAlive == true;

    private void MessageLoop()
    {
        try
        {
            var className = $"MercMapperRawInputWindow-{Guid.NewGuid():N}";
            var windowClass = new WindowClassEx
            {
                Size = (uint)Marshal.SizeOf<WindowClassEx>(),
                WindowProcedure = _windowProcedure,
                ClassName = className
            };

            if (RawInputApi.RegisterClassEx(ref windowClass) == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            _windowHandle = RawInputApi.CreateWindowEx(
                0,
                className,
                "Merc Mapper Raw Input Window",
                RawInputApi.WsOverlapped,
                0,
                0,
                0,
                0,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_windowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"CreateWindowEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            var devices = new[]
            {
                new RawInputDevice
                {
                    UsagePage = 0x01,
                    Usage = 0x06,
                    Flags = RawInputApi.RidevInputSink,
                    TargetWindow = _windowHandle
                },
                new RawInputDevice
                {
                    UsagePage = 0x0C,
                    Usage = 0x01,
                    Flags = RawInputApi.RidevInputSink | RawInputApi.RidevNoLegacy,
                    TargetWindow = _windowHandle
                }
            };

            if (!RawInputApi.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
            {
                var noLegacyError = Marshal.GetLastWin32Error();
                _log($"Raw Input registration with RIDEV_NOLEGACY failed with Win32 error {noLegacyError}; retrying without NOLEGACY. Consumer-control shell actions may still leak through.");
                devices[1].Flags = RawInputApi.RidevInputSink;

                if (!RawInputApi.RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>()))
                {
                    throw new InvalidOperationException($"RegisterRawInputDevices failed with Win32 error {Marshal.GetLastWin32Error()}.");
                }
            }

            _hookHandle = RawInputApi.SetWindowsHookEx(RawInputApi.KeyboardLowLevelHookId, _hookProcedure, IntPtr.Zero, 0);
            if (_hookHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException($"SetWindowsHookEx failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }

            _shellSuppressor = new ShellAppCommandSuppressor(_log);
            _shellSuppressor.Start();

            if (_options.EnableRepeat)
            {
                _repeatTimer = new Timer(RepeatHeldKeys, null, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(_options.RepeatRateMs));
            }

            _started.Set();
            _log("Raw Input listener and suppression hook active.");

            int result;
            while ((result = RawInputApi.GetMessage(out var message, IntPtr.Zero, 0, 0)) > 0)
            {
                RawInputApi.TranslateMessage(ref message);
                RawInputApi.DispatchMessage(ref message);
            }

            if (result < 0)
            {
                throw new InvalidOperationException($"GetMessage failed with Win32 error {Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception ex)
        {
            if (!_started.IsSet)
            {
                _startupException = ex;
                _started.Set();
            }
            else
            {
                _stopReason = ex.Message;
            }

            _log($"Mapper listener failed: {ex.Message}");
        }
        finally
        {
            _shellSuppressor?.Dispose();
            _shellSuppressor = null;

            if (_hookHandle != IntPtr.Zero)
            {
                RawInputApi.UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            if (_windowHandle != IntPtr.Zero)
            {
                RawInputApi.DestroyWindow(_windowHandle);
                _windowHandle = IntPtr.Zero;
            }

            Stopped?.Invoke(_stopReason);
        }
    }

    private void DisposeStartedEvent()
    {
        if (_startedDisposed)
        {
            return;
        }

        _startedDisposed = true;
        _started.Dispose();
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        if (message == RawInputApi.WmClose)
        {
            RawInputApi.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        if (message == RawInputApi.WmInput)
        {
            ReadRawInput(lParam);
            return IntPtr.Zero;
        }

        if (message == RawInputApi.WmAppCommand)
        {
            return new IntPtr(1);
        }

        return RawInputApi.DefWindowProc(hwnd, message, wParam, lParam);
    }

    private IntPtr HookProcedure(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code != RawInputApi.HookAction)
        {
            return RawInputApi.CallNextHookEx(_hookHandle, code, wParam, lParam);
        }

        var hook = Marshal.PtrToStructure<KeyboardLowLevelHook>(lParam);
        if ((hook.Flags & RawInputApi.LlkHfInjected) != 0)
        {
            return RawInputApi.CallNextHookEx(_hookHandle, code, wParam, lParam);
        }

        LogCrouchCandidate(wParam, hook);

        var virtualKey = (ushort)hook.VirtualKeyCode;
        if (SuppressedSourceKeys.Contains(virtualKey))
        {
            if (SourceToTarget.TryGetValue(virtualKey, out var targetKey))
            {
                if (MercMappingPolicy.IsTargetEnabled(_options, targetKey))
                {
                    var message = (uint)wParam.ToInt32();
                    var isKeyUp = message is RawInputApi.WmKeyUp or RawInputApi.WmSystemKeyUp;
                    SetMappedKey(targetKey, !isKeyUp, virtualKey, source: "hook", sourceId: $"hook:{hook.VirtualKeyCode:X2}");
                }
            }

            return new IntPtr(1);
        }

        if (MercMappingPolicy.ShouldSuppressLegacyForRawOnlySource(virtualKey))
        {
            return new IntPtr(1);
        }

        var sourceKey = new KeyboardSourceKey(
            virtualKey,
            hook.ScanCode,
            (hook.Flags & RawInputApi.LlkHfExtended) != 0);

        if (_options.EnableKeypadCluster && ScanSourceToTarget.TryGetValue(sourceKey, out var scanTargetKey))
        {
            var message = (uint)wParam.ToInt32();
            var isKeyUp = message is RawInputApi.WmKeyUp or RawInputApi.WmSystemKeyUp;
            SetMappedKey(scanTargetKey, !isKeyUp, (ushort)hook.VirtualKeyCode, source: $"hook scan=0x{hook.ScanCode:X2}", sourceId: $"scan:{hook.VirtualKeyCode:X2}:{hook.ScanCode:X2}:{sourceKey.Extended}");
            return new IntPtr(1);
        }

        return RawInputApi.CallNextHookEx(_hookHandle, code, wParam, lParam);
    }

    private void LogCrouchCandidate(IntPtr wParam, KeyboardLowLevelHook hook)
    {
        if (hook.VirtualKeyCode != VirtualKeys.Delete &&
            hook.VirtualKeyCode != VirtualKeys.Decimal &&
            hook.VirtualKeyCode != VirtualKeys.Period &&
            hook.ScanCode != 0x53)
        {
            return;
        }

        var message = (uint)wParam.ToInt32();
        var isKeyUp = message is RawInputApi.WmKeyUp or RawInputApi.WmSystemKeyUp;
        var extended = (hook.Flags & RawInputApi.LlkHfExtended) != 0;
        _log($"{DateTimeOffset.Now:HH:mm:ss.fff} crouch-candidate hook vk=0x{hook.VirtualKeyCode:X2} scan=0x{hook.ScanCode:X2} extended={extended} {(isKeyUp ? "up" : "down")}");
    }

    private void ReadRawInput(IntPtr rawInputHandle)
    {
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint size = 0;
        RawInputApi.GetRawInputData(rawInputHandle, RawInputApi.RidInput, IntPtr.Zero, ref size, headerSize);
        if (size == 0)
        {
            return;
        }

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            var read = RawInputApi.GetRawInputData(rawInputHandle, RawInputApi.RidInput, buffer, ref size, headerSize);
            if (read != size)
            {
                _log($"GetRawInputData returned {read}, expected {size}.");
                return;
            }

            var input = Marshal.PtrToStructure<RawInput>(buffer);
            if (input.Header.Type == RawInputApi.RimTypeHid)
            {
                ReadRawHid(input.Header, buffer);
                return;
            }

            if (input.Header.Type != RawInputApi.RimTypeKeyboard)
            {
                return;
            }

            var deviceName = _deviceNames.GetOrAdd(input.Header.Device, GetDeviceName);
            if (!deviceName.Contains("VID_1038&PID_0210", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var rawSourceKey = new KeyboardSourceKey(
                input.Keyboard.VKey,
                input.Keyboard.MakeCode,
                (input.Keyboard.Flags & RawInputApi.RiKeyE0) != 0);

            var hasDirectTarget = SourceToTarget.TryGetValue(input.Keyboard.VKey, out var directTarget) &&
                                  MercMappingPolicy.IsTargetEnabled(_options, directTarget);
            var hasScanTarget = ScanSourceToTarget.TryGetValue(rawSourceKey, out var scanTarget) &&
                                MercMappingPolicy.ShouldMapScanSource(_options, rawSourceKey);

            if (!hasDirectTarget && !hasScanTarget)
            {
                return;
            }

            var isKeyUp = input.Keyboard.Message is RawInputApi.WmKeyUp or RawInputApi.WmSystemKeyUp ||
                          (input.Keyboard.Flags & RawInputApi.RiKeyBreak) != 0;

            if (hasDirectTarget && MercMappingPolicy.IsRawOnlySource(input.Keyboard.VKey))
            {
                SetMappedKey(directTarget, !isKeyUp, input.Keyboard.VKey, source: "raw", sourceId: $"raw:{input.Header.Device}:{input.Keyboard.VKey:X2}");
            }

            if (hasScanTarget)
            {
                SetMappedKey(scanTarget, !isKeyUp, input.Keyboard.VKey, source: $"raw scan=0x{input.Keyboard.MakeCode:X2}", sourceId: $"raw-scan:{input.Header.Device}:{input.Keyboard.VKey:X2}:{input.Keyboard.MakeCode:X2}:{rawSourceKey.Extended}");
            }

            _log($"{DateTimeOffset.Now:HH:mm:ss.fff} Merc raw confirmed 0x{input.Keyboard.VKey:X2} {(isKeyUp ? "up" : "down")}");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void SetMappedKey(ushort targetKey, bool down, ushort sourceKey, string source, string sourceId)
    {
        KeyTransition transition;
        lock (_stateLock)
        {
            transition = _keyState.Apply(targetKey, sourceId, down, _options.EnableRepeat, DateTimeOffset.UtcNow, _options.InitialRepeatDelayMs);
        }

        if (!transition.Changed)
        {
            return;
        }

        if (transition.ShouldSend && !TrySendKeyboardInput(targetKey, down, out var error))
        {
            if (down)
            {
                lock (_stateLock)
                {
                    _keyState.RollBackDown(targetKey, sourceId);
                }
            }

            _log($"{DateTimeOffset.Now:HH:mm:ss.fff} SendInput failed for 0x{targetKey:X2} {(down ? "down" : "up")}: {error}");
            return;
        }

        if (transition.ShouldSend)
        {
            _log($"{DateTimeOffset.Now:HH:mm:ss.fff} {source} 0x{sourceKey:X2} -> 0x{targetKey:X2} {(down ? "down" : "up")}");
        }
    }

    private void ReadRawHid(RawInputHeader header, IntPtr buffer)
    {
        var deviceName = _deviceNames.GetOrAdd(header.Device, GetDeviceName);
        if (!deviceName.Contains("VID_1038&PID_0210", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rawHidOffset = Marshal.SizeOf<RawInputHeader>();
        var sizeHid = Marshal.ReadInt32(buffer, rawHidOffset);
        var count = Marshal.ReadInt32(buffer, rawHidOffset + 4);
        if (sizeHid <= 0 || count <= 0)
        {
            return;
        }

        var dataOffset = rawHidOffset + 8;
        for (var index = 0; index < count; index++)
        {
            var report = new byte[sizeHid];
            Marshal.Copy(IntPtr.Add(buffer, dataOffset + (index * sizeHid)), report, 0, report.Length);
            HandleConsumerReport(report);
        }
    }

    private void HandleConsumerReport(byte[] report)
    {
        if (report.Length < 4 || report[0] != 0x01)
        {
            _log($"{DateTimeOffset.Now:HH:mm:ss.fff} Merc HID unmapped report {Convert.ToHexString(report)}");
            return;
        }

        foreach (var mapping in ConsumerBitMappings)
        {
            if (!_options.EnableQ && mapping.TargetVirtualKey == VirtualKeys.Q)
            {
                continue;
            }

            if (!MercMappingPolicy.IsTargetEnabled(_options, mapping.TargetVirtualKey))
            {
                continue;
            }

            var reportHex = Convert.ToHexString(report);
            var down = (report[mapping.ByteIndex] & mapping.Mask) != 0;
            SetMappedKey(mapping.TargetVirtualKey, down, sourceKey: 0, source: $"hid {reportHex}", sourceId: $"hid:{mapping.ByteIndex}:{mapping.Mask:X2}");
        }
    }

    private void ReleaseAllMappedKeys(bool log = false)
    {
        ushort[] keys;
        lock (_stateLock)
        {
            keys = _keyState.ReleaseAll();
        }

        foreach (var key in keys)
        {
            if (!TrySendKeyboardInput(key, down: false, out var error))
            {
                _log($"{DateTimeOffset.Now:HH:mm:ss.fff} release failed for 0x{key:X2}: {error}");
                continue;
            }

            if (log)
            {
                _log($"{DateTimeOffset.Now:HH:mm:ss.fff} hid release -> 0x{key:X2} up");
            }
        }
    }

    private static bool TrySendKeyboardInput(ushort virtualKey, bool down, out string error)
    {
        error = string.Empty;
        var keyboard = TargetScanCodes.TryGetValue(virtualKey, out var scanCode)
            ? new KeyboardInput
            {
                VirtualKey = 0,
                Scan = scanCode,
                Flags = RawInputApi.KeyEventFScancode | (down ? 0u : RawInputApi.KeyEventFKeyUp)
            }
            : new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = down ? 0u : RawInputApi.KeyEventFKeyUp
            };

        var input = new Input
        {
            Type = RawInputApi.InputKeyboard,
            Keyboard = keyboard
        };

        var sent = RawInputApi.SendInput(1, [input], Marshal.SizeOf<Input>());
        if (sent != 1)
        {
            error = $"Win32 error {Marshal.GetLastWin32Error()}";
            return false;
        }

        return true;
    }

    private void RepeatHeldKeys(object? state)
    {
        ushort[] dueKeys;
        lock (_stateLock)
        {
            var now = DateTimeOffset.UtcNow;
            dueKeys = _keyState.TakeDueRepeats(now, _options.RepeatRateMs);
        }

        foreach (var key in dueKeys)
        {
            if (!TrySendKeyboardInput(key, down: true, out var error))
            {
                _log($"{DateTimeOffset.Now:HH:mm:ss.fff} repeat failed for 0x{key:X2}: {error}");
            }
        }
    }

    private static string GetDeviceName(IntPtr deviceHandle)
    {
        uint size = 0;
        RawInputApi.GetRawInputDeviceInfo(deviceHandle, RawInputApi.RidiDeviceName, null, ref size);
        if (size == 0)
        {
            return "(unknown)";
        }

        var builder = new StringBuilder((int)size);
        var result = RawInputApi.GetRawInputDeviceInfo(deviceHandle, RawInputApi.RidiDeviceName, builder, ref size);
        return result == uint.MaxValue ? "(unknown)" : builder.ToString();
    }

    private readonly record struct ConsumerBitMapping(int ByteIndex, byte Mask, ushort TargetVirtualKey);
}
