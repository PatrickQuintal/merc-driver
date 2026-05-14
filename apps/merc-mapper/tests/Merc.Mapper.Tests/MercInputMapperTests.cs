using System.Reflection;

namespace Merc.Mapper.Tests;

public sealed class MercInputMapperTests
{
    [Fact]
    public void JumpAndWalkAreMappedAndSuppressedByTheHookPath()
    {
        var sourceToTarget = GetPrivateStatic<IReadOnlyDictionary<ushort, ushort>>("SourceToTarget");
        var suppressedSourceKeys = GetPrivateStatic<IReadOnlySet<ushort>>("SuppressedSourceKeys");

        Assert.Equal(VirtualKeys.Space, sourceToTarget[VirtualKeys.Multiply]);
        Assert.Equal(VirtualKeys.LeftShift, sourceToTarget[VirtualKeys.Divide]);
        Assert.Contains(VirtualKeys.Multiply, suppressedSourceKeys);
        Assert.Contains(VirtualKeys.Divide, suppressedSourceKeys);
    }

    [Fact]
    public void CrouchAndRoundKeysAreMappedThroughTheRealScanTable()
    {
        var scanSourceToTarget = GetPrivateStatic<IReadOnlyDictionary<KeyboardSourceKey, ushort>>("ScanSourceToTarget");

        Assert.Equal(VirtualKeys.LeftControl, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Delete, 0x53, false)]);
        Assert.Equal(VirtualKeys.LeftControl, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Decimal, 0x53, false)]);
        Assert.Equal(VirtualKeys.Key7, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Home, 0x47, false)]);
        Assert.Equal(VirtualKeys.Key8, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Up, 0x48, false)]);
        Assert.Equal(VirtualKeys.Key9, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.PageUp, 0x49, false)]);
        Assert.Equal(VirtualKeys.Key0, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Insert, 0x52, false)]);
        Assert.Equal(VirtualKeys.OemPlus, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Add, 0x4E, false)]);
        Assert.Equal(VirtualKeys.Key1, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.End, 0x4F, false)]);
        Assert.Equal(VirtualKeys.Key2, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Down, 0x50, false)]);
        Assert.Equal(VirtualKeys.Key3, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.PageDown, 0x51, false)]);
        Assert.Equal(VirtualKeys.Key4, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Left, 0x4B, false)]);
        Assert.Equal(VirtualKeys.Key5, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Clear, 0x4C, false)]);
        Assert.Equal(VirtualKeys.Key6, scanSourceToTarget[new KeyboardSourceKey(VirtualKeys.Right, 0x4D, false)]);
    }

    private static T GetPrivateStatic<T>(string fieldName)
    {
        var field = typeof(MercInputMapper).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(null));
    }
}
