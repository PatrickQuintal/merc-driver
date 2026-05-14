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

    private static T GetPrivateStatic<T>(string fieldName)
    {
        var field = typeof(MercInputMapper).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        return Assert.IsAssignableFrom<T>(field.GetValue(null));
    }
}
