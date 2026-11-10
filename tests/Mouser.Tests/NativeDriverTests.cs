using System.Runtime.InteropServices;
using Mouser.Core.Interop;
using Xunit;

namespace Mouser.Tests;

public class NativeDriverTests
{
    static NativeDriverTests()
    {
        // Ensure native library resolution path on macOS / Linux / Windows
        NativeLibrary.SetDllImportResolver(typeof(NativeDriver).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName == "mouser_native")
            {
                string basePath = AppContext.BaseDirectory;
                string nativeFileName = OperatingSystem.IsMacOS() ? "libmouser_native.dylib" :
                                        OperatingSystem.IsWindows() ? "mouser_native.dll" : "libmouser_native.so";

                string localPath = Path.Combine(basePath, nativeFileName);
                if (File.Exists(localPath))
                {
                    return NativeLibrary.Load(localPath);
                }

                // Fallback to bin/native
                string binNativePath = Path.GetFullPath(Path.Combine(basePath, "../../../../../bin/native", nativeFileName));
                if (File.Exists(binNativePath))
                {
                    return NativeLibrary.Load(binNativePath);
                }
            }

            return IntPtr.Zero;
        });
    }

    [Fact]
    public void NativePing_ReturnsMultipliedValue()
    {
        int result = NativeDriver.NativePing(3);
        Assert.Equal(126, result); // 3 * 42 = 126
    }

    [Fact]
    public void GetNativeVersion_ReturnsExpectedVersion()
    {
        uint version = NativeDriver.GetNativeVersion();
        Assert.Equal(0x00010000u, version); // 1.0.0
    }

    [Fact]
    public void CheckAccessibilityPermission_DoesNotThrow()
    {
        bool isTrusted = NativeDriver.CheckAccessibilityPermission();
        // Result is boolean, should execute cleanly
        Assert.True(isTrusted == true || isTrusted == false);
    }
}
