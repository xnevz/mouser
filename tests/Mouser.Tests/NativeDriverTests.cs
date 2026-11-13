using Mouser.Core.Interop;
using Xunit;

namespace Mouser.Tests;

/// <summary>Runs only where libmouser_native has been built (cmake in src/Mouser.Native).</summary>
public sealed class NativeFactAttribute : FactAttribute
{
    public NativeFactAttribute()
    {
        string name = OperatingSystem.IsMacOS() ? "libmouser_native.dylib" :
                      OperatingSystem.IsWindows() ? "mouser_native.dll" : "libmouser_native.so";
        // Mouser.Core.csproj copies bin/native/* next to the test assembly once it is built.
        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, name))) Skip = "Native library not built";
    }
}

public class NativeDriverTests
{
    [NativeFact]
    public void NativePing_ReturnsMultipliedValue()
    {
        int result = NativeDriver.NativePing(3);
        Assert.Equal(126, result); // 3 * 42 = 126
    }

    [NativeFact]
    public void GetNativeVersion_ReturnsExpectedVersion()
    {
        uint version = NativeDriver.GetNativeVersion();
        Assert.Equal(0x00010000u, version); // 1.0.0
    }

    [NativeFact]
    public void CheckAccessibilityPermission_DoesNotThrow()
    {
        bool isTrusted = NativeDriver.CheckAccessibilityPermission();
        // Result is boolean, should execute cleanly
        Assert.True(isTrusted == true || isTrusted == false);
    }
}
