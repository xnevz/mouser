# Phase 0: macOS Development Environment & Prerequisites

> **Objective**: Prepare the macOS workstation for developing a hybrid .NET 8/9 C# application that compiles and loads low-level Objective-C++ dynamic libraries (`.dylib`) with full macOS TCC (Accessibility & Input Monitoring) permissions.

---

## 1. Required Toolchains & SDKs

### 1.1 .NET 8 or 9 SDK (Native ARM64 / x64)
- **Installation**:
  ```bash
  # Check if dotnet is already installed
  dotnet --info

  # If not installed, install via Homebrew or official Microsoft installer:
  brew install --cask dotnet-sdk
  ```
- **Verify Architecture**:
  Ensure the SDK matches your Apple Silicon (`osx-arm64`) or Intel (`osx-x64`) processor:
  ```bash
  dotnet --version # Should be 8.0.xxx or 9.0.xxx
  ```

### 1.2 Xcode Command Line Tools & Native Compilers
Required to compile the Objective-C++ input capture hooks (`libmouser_mac.mm` $\rightarrow$ `libmouser_mac.dylib`):
```bash
# Install Xcode Command Line Tools (provides clang, clang++, make)
xcode-select --install

# Verify clang compiler
clang++ --version
```

### 1.3 CMake & Build System
Used to manage the cross-platform native C/C++ compilation:
```bash
brew install cmake ninja
cmake --version
```

### 1.4 Recommended IDE & Extensions
- **Option A**: **JetBrains Rider** (industry standard for cross-platform .NET, full support for Avalonia XAML previewer and native C++ profiling).
- **Option B**: **Visual Studio Code** with:
  - C# Dev Kit (`ms-dotnettools.csdevkit`)
  - Avalonia for VS Code (`AvaloniaTeam.vscode-avalonia`)
  - CMake Tools (`ms-vscode.cmake-tools`)

---

## 2. macOS Permissions & Security Model (TCC)

Because Mouser acts as a Virtual KVM, it captures global mouse/keyboard events and injects synthetic events. macOS requires explicit user approval under **TCC (Transparency, Consent, and Control)**:

### 2.1 Required Permissions
1. **Accessibility (`AXIsProcessTrustedWithOptions`)**:
   - Required for `CGEventTapCreate` to intercept, modify, and swallow mouse and keyboard events.
   - Without this, the event tap returns `NULL`.
2. **Input Monitoring**:
   - Required by macOS Catalina (10.15)+ to observe keystrokes globally outside the app window.

### 2.2 Enabling Permissions for Local Development
During development in terminal or IDE:
1. Open **System Settings $\rightarrow$ Privacy & Security $\rightarrow$ Accessibility**.
2. Add and toggle **ON**:
   - Your Terminal app (e.g. `Terminal.app`, `iTerm.app`, or `Alacritty`).
   - Your IDE (e.g. `Visual Studio Code` or `Rider`).
   - Once the final `Mouser.app` bundle is generated, add `Mouser.app`.

---

## 3. Step-by-Step Verification Test

To verify that .NET can compile, load, and execute an Objective-C++ native function on macOS:

### Step 3.1: Create a Minimal Native Test Library
Create a test file `test_native.mm`:
```objective-c
#import <Foundation/Foundation.h>

extern "C" {
    int mouser_test_ping(int value) {
        NSLog(@"[Native macOS] Ping received: %d", value);
        return value * 42;
    }
}
```

Compile it to dynamic library:
```bash
clang++ -shared -fPIC -framework Foundation -o libtest_native.dylib test_native.mm
```

### Step 3.2: Verify C# `[LibraryImport]` Loads the Library
> [!NOTE]
> `[LibraryImport]` source generators require `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>` in your `.csproj`.

In C#:
```csharp
using System.Runtime.InteropServices;

public static partial class TestNative
{
    [LibraryImport("libtest_native.dylib", EntryPoint = "mouser_test_ping")]
    public static partial int Ping(int value);
}

// Execution
int result = TestNative.Ping(2);
Console.WriteLine($"Result from native dylib: {result}"); // Output: 84
```

---

## 4. Phase 0 Deliverables & Exit Criteria

- [x] .NET 8/9 SDK confirmed working on macOS (`dotnet --version` -> `8.0.428`).
- [x] Xcode command line tools and `clang++` verified (Apple clang 21.0.0).
- [x] CMake installed and verified (`cmake 3.31.0`).
- [x] Environment configured (`DOTNET_ROOT` and PATH symlinked to `~/.local/bin`).
- [x] Minimal native `.dylib` successfully built and invoked from a .NET console harness (`Ping(2) == 84`).
- [ ] Terminal and IDE granted Accessibility permissions in macOS System Settings.
