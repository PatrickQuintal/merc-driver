namespace Merc.Mapper.Tests;

public sealed class MercMappingPolicyTests
{
    [Fact]
    public void IsTargetEnabledDisablesOnlyQWhenOptionIsOff()
    {
        var options = new MapperOptions(EnableQ: false);

        Assert.False(MercMappingPolicy.IsTargetEnabled(options, VirtualKeys.Q));
        Assert.True(MercMappingPolicy.IsTargetEnabled(options, VirtualKeys.W));
    }

    [Theory]
    [InlineData(VirtualKeys.Multiply)]
    [InlineData(VirtualKeys.Divide)]
    public void IsRawOnlySourceAllowsOnlyMultiplyAndDivide(ushort virtualKey)
    {
        Assert.True(MercMappingPolicy.IsRawOnlySource(virtualKey));
    }

    [Fact]
    public void IsRawOnlySourceRejectsBrowserAndNormalSources()
    {
        Assert.False(MercMappingPolicy.IsRawOnlySource(VirtualKeys.BrowserBack));
        Assert.False(MercMappingPolicy.IsRawOnlySource(VirtualKeys.A));
    }

    [Fact]
    public void RawOnlySourcesSuppressLegacyEventSoOriginalKeyDoesNotLeak()
    {
        Assert.True(MercMappingPolicy.ShouldSuppressLegacyForRawOnlySource(VirtualKeys.Multiply));
        Assert.True(MercMappingPolicy.ShouldSuppressLegacyForRawOnlySource(VirtualKeys.Divide));
        Assert.False(MercMappingPolicy.ShouldSuppressLegacyForRawOnlySource(VirtualKeys.A));
    }

    [Theory]
    [InlineData(VirtualKeys.Multiply, 0x37u, false)]
    [InlineData(VirtualKeys.Divide, 0x35u, true)]
    [InlineData(VirtualKeys.Clear, 0x59u, false)]
    public void ShouldMapRawScanSourceAllowsKnownMercRawScanKeys(ushort virtualKey, uint scanCode, bool extended)
    {
        Assert.True(MercMappingPolicy.ShouldMapRawScanSource(new KeyboardSourceKey(virtualKey, scanCode, extended)));
    }

    [Theory]
    [InlineData(VirtualKeys.Multiply, 0x37u, true)]
    [InlineData(VirtualKeys.Divide, 0x35u, false)]
    [InlineData(VirtualKeys.Clear, 0x4Cu, false)]
    public void ShouldMapRawScanSourceRejectsNonMatchingScanShape(ushort virtualKey, uint scanCode, bool extended)
    {
        Assert.False(MercMappingPolicy.ShouldMapRawScanSource(new KeyboardSourceKey(virtualKey, scanCode, extended)));
    }

    [Fact]
    public void ShouldMapScanSourceRequiresKeypadClusterUnlessRawScanSourceIsKnown()
    {
        var keypadSource = new KeyboardSourceKey(VirtualKeys.Home, 0x47, false);
        var safeRawSource = new KeyboardSourceKey(VirtualKeys.Multiply, 0x37, false);

        Assert.False(MercMappingPolicy.ShouldMapScanSource(new MapperOptions(EnableKeypadCluster: false), keypadSource));
        Assert.True(MercMappingPolicy.ShouldMapScanSource(new MapperOptions(EnableKeypadCluster: true), keypadSource));
        Assert.True(MercMappingPolicy.ShouldMapScanSource(new MapperOptions(EnableKeypadCluster: false), safeRawSource));
    }
}
