namespace Merc.Mapper;

public sealed record KeyMappingInfo(
    string PhysicalKey,
    string EmittedKey,
    bool HasCaveat = false,
    string? Caveat = null);

public static class KeyMappingCatalog
{
    public const string RoundNumberCaveat = "Hardware primary is keypad/home-cluster, game-dependent.";

    public static IReadOnlyList<KeyMappingInfo> All { get; } =
    [
        new("gamepad-q", "Q"),
        new("gamepad-w", "W"),
        new("gamepad-e", "E"),
        new("gamepad-a", "A"),
        new("gamepad-s", "S"),
        new("gamepad-d", "D"),
        new("gamepad-reload-r", "R"),
        new("gamepad-tab", "Tab"),
        new("top-z", "Z"),
        new("gamepad-2-t", "T"),
        new("gamepad-use-f", "F"),
        new("gamepad-3-g", "G"),
        new("gamepad-4-v", "V"),
        new("gamepad-5-b", "B"),
        new("gamepad-6-c", "C"),
        new("gamepad-jump-space", "Space"),
        new("gamepad-walk-shift", "Shift"),
        new("gamepad-duck-ctrl", "Left Ctrl"),
        new("gamepad-round-7", "7", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-8", "8", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-9", "9", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-10", "0", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-11", "=", HasCaveat: true, "Hardware primary is keypad add, game-dependent."),
        new("gamepad-round-1", "1", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-2", "2", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-3", "3", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-4", "4", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-5", "5", HasCaveat: true, RoundNumberCaveat),
        new("gamepad-round-6", "6", HasCaveat: true, RoundNumberCaveat),
    ];
}
