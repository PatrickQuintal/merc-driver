namespace Merc.Mapper.Tests;

public sealed class KeyMappingCatalogTests
{
    [Fact]
    public void CatalogContainsExpectedProductionMappings()
    {
        Assert.Contains(KeyMappingCatalog.All, mapping => mapping.PhysicalKey == "gamepad-w" && mapping.EmittedKey == "W");
        Assert.Contains(KeyMappingCatalog.All, mapping => mapping.PhysicalKey == "gamepad-duck-ctrl" && mapping.EmittedKey == "Left Ctrl");
        Assert.Contains(KeyMappingCatalog.All, mapping => mapping.PhysicalKey == "gamepad-round-1" && mapping.EmittedKey == "1" && mapping.HasCaveat);
        Assert.Contains(KeyMappingCatalog.All, mapping => mapping.PhysicalKey == "gamepad-round-11" && mapping.EmittedKey == "=" && mapping.Caveat == "Hardware primary is keypad add, game-dependent.");
    }

    [Fact]
    public void RoundNumberMappingsHaveCaveats()
    {
        var roundMappings = KeyMappingCatalog.All.Where(mapping => mapping.PhysicalKey.StartsWith("gamepad-round-", StringComparison.Ordinal)).ToArray();

        Assert.NotEmpty(roundMappings);
        Assert.All(roundMappings, mapping => Assert.True(mapping.HasCaveat));
        Assert.All(roundMappings, mapping => Assert.False(string.IsNullOrWhiteSpace(mapping.Caveat)));
    }

    [Fact]
    public void PhysicalKeysAreUnique()
    {
        var duplicates = KeyMappingCatalog.All
            .GroupBy(mapping => mapping.PhysicalKey)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        Assert.Empty(duplicates);
    }
}
