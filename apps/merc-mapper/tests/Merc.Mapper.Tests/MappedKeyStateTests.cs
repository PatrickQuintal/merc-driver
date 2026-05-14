namespace Merc.Mapper.Tests;

public sealed class MappedKeyStateTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FirstSourceDownSendsKeyDown()
    {
        var state = new MappedKeyState();

        var transition = state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, repeatDelayMs: 350);

        Assert.Equal(new KeyTransition(Changed: true, ShouldSend: true), transition);
        Assert.True(state.IsDown(VirtualKeys.W));
    }

    [Fact]
    public void DuplicateDownFromSameSourceDoesNotSendAgain()
    {
        var state = new MappedKeyState();

        state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, repeatDelayMs: 350);
        var transition = state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, repeatDelayMs: 350);

        Assert.Equal(new KeyTransition(Changed: false, ShouldSend: false), transition);
    }

    [Fact]
    public void MultipleSourcesForSameTargetSendUpOnlyAfterLastRelease()
    {
        var state = new MappedKeyState();

        Assert.True(state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, 350).ShouldSend);
        Assert.False(state.Apply(VirtualKeys.W, "source-b", down: true, repeatEnabled: false, Now, 350).ShouldSend);
        Assert.False(state.Apply(VirtualKeys.W, "source-a", down: false, repeatEnabled: false, Now, 350).ShouldSend);

        var finalRelease = state.Apply(VirtualKeys.W, "source-b", down: false, repeatEnabled: false, Now, 350);

        Assert.Equal(new KeyTransition(Changed: true, ShouldSend: true), finalRelease);
        Assert.False(state.IsDown(VirtualKeys.W));
    }

    [Fact]
    public void DuplicateUpAfterReleaseDoesNothing()
    {
        var state = new MappedKeyState();

        state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, 350);
        state.Apply(VirtualKeys.W, "source-a", down: false, repeatEnabled: false, Now, 350);
        var duplicateUp = state.Apply(VirtualKeys.W, "source-a", down: false, repeatEnabled: false, Now, 350);

        Assert.Equal(new KeyTransition(Changed: false, ShouldSend: false), duplicateUp);
    }

    [Fact]
    public void RollBackDownClearsFailedFirstDown()
    {
        var state = new MappedKeyState();

        state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, 350);
        state.RollBackDown(VirtualKeys.W, "source-a");

        Assert.False(state.IsDown(VirtualKeys.W));
        Assert.Equal(new KeyTransition(Changed: false, ShouldSend: false), state.Apply(VirtualKeys.W, "source-a", down: false, repeatEnabled: false, Now, 350));
    }

    [Fact]
    public void ReleaseAllReturnsEachTrackedTargetAndClearsState()
    {
        var state = new MappedKeyState();
        state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: false, Now, 350);
        state.Apply(VirtualKeys.A, "source-b", down: true, repeatEnabled: false, Now, 350);
        state.Apply(VirtualKeys.A, "source-c", down: true, repeatEnabled: false, Now, 350);

        var released = state.ReleaseAll();

        Assert.Equal(new ushort[] { VirtualKeys.A, VirtualKeys.W }, released.Order().ToArray());
        Assert.False(state.IsDown(VirtualKeys.W));
        Assert.False(state.IsDown(VirtualKeys.A));
    }

    [Fact]
    public void TakeDueRepeatsReturnsOnlyDueHeldKeysAndAdvancesSchedule()
    {
        var state = new MappedKeyState();
        state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: true, Now, repeatDelayMs: 100);
        state.Apply(VirtualKeys.A, "source-b", down: true, repeatEnabled: true, Now, repeatDelayMs: 200);

        Assert.Empty(state.TakeDueRepeats(Now.AddMilliseconds(99), repeatRateMs: 50));
        Assert.Equal(new ushort[] { VirtualKeys.W }, state.TakeDueRepeats(Now.AddMilliseconds(100), repeatRateMs: 50));
        Assert.Empty(state.TakeDueRepeats(Now.AddMilliseconds(149), repeatRateMs: 50));
        Assert.Equal(new ushort[] { VirtualKeys.W }, state.TakeDueRepeats(Now.AddMilliseconds(150), repeatRateMs: 50));
    }

    [Fact]
    public void ReleasingKeyClearsRepeatSchedule()
    {
        var state = new MappedKeyState();
        state.Apply(VirtualKeys.W, "source-a", down: true, repeatEnabled: true, Now, repeatDelayMs: 100);
        state.Apply(VirtualKeys.W, "source-a", down: false, repeatEnabled: true, Now.AddMilliseconds(50), repeatDelayMs: 100);

        Assert.Empty(state.TakeDueRepeats(Now.AddMilliseconds(100), repeatRateMs: 50));
    }
}
