namespace Merc.Mapper;

internal static class MercMappingPolicy
{
    public static bool IsTargetEnabled(MapperOptions options, ushort targetKey)
    {
        return options.EnableQ || targetKey != VirtualKeys.Q;
    }

    public static bool IsRawOnlySource(ushort virtualKey)
    {
        return virtualKey is VirtualKeys.Multiply or VirtualKeys.Divide;
    }

    public static bool ShouldSuppressLegacyForRawOnlySource(ushort virtualKey)
    {
        return IsRawOnlySource(virtualKey);
    }

    public static bool ShouldMapRawScanSource(KeyboardSourceKey sourceKey)
    {
        return sourceKey is { VirtualKey: VirtualKeys.Multiply, ScanCode: 0x37, Extended: false } ||
               sourceKey is { VirtualKey: VirtualKeys.Divide, ScanCode: 0x35, Extended: true } ||
               sourceKey is { VirtualKey: VirtualKeys.Clear, ScanCode: 0x59, Extended: false };
    }

    public static bool ShouldMapScanSource(MapperOptions options, KeyboardSourceKey sourceKey)
    {
        return options.EnableKeypadCluster || ShouldMapRawScanSource(sourceKey);
    }
}
