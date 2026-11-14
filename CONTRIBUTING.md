# Contributing to Mouser

Thank you for your interest in contributing to Mouser! We are committed to building an ultra-fast, robust, and secure mouse, keyboard, and clipboard sharing solution.

---

## 1. Development Prerequisites

- **OS:** Windows 10 (1809+) or Windows 11.
- **SDK:** [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
- **IDE:** Visual Studio 2022, JetBrains Rider, or VS Code with C# Dev Kit.

---

## 2. Building & Running Tests

### Build the Solution
```powershell
dotnet build Mouser.sln
```

### Run Fast Test Suite
To run the automated test suite (excluding benchmark tests):
```powershell
dotnet test tests/Mouser.Tests --filter "Category!=Perf" --nologo -v q
```

### Run Performance Budget Tests
```powershell
dotnet test tests/Mouser.Tests --filter "Category=Perf" --nologo -v q
```

---

## 3. Code Standards & Architecture Guidelines

1. **Layer Boundary:** `Mouser.Core` must **never** reference `Mouser.UI` or Avalonia. Pure logic, models, networking, and cryptography belong in Core.
2. **UI Theme Tokens:** Never hardcode raw hex colors in `.axaml` files. Always reference dynamic theme tokens:
   ```xml
   <!-- Correct -->
   <Border Background="{DynamicResource BgSurface}" BorderBrush="{DynamicResource BorderSubtle}" />
   
   <!-- Disallowed -->
   <Border Background="#13141d" />
   ```
3. **No Unapproved Packages:** Do not add new NuGet dependencies without approval. The codebase relies heavily on the .NET base class library for performance and security.
4. **Deterministic Testing:** Tests must never use arbitrary `Thread.Sleep`. Use `Wait.UntilAsync` and dependency-injected clocks/fakes (`FakeCapture`, `FakeClipboard`, `RecordingSink`).
5. **Git Discipline & Conventional Commits:**
   Format commit messages as: `<type>(<scope>): <description>`  
   Types: `feat`, `fix`, `test`, `refactor`, `docs`, `chore`.

---

## 4. Implementing a New Platform Backend

Mouser abstracts OS-specific input and clipboard handling behind three core interfaces in `Mouser.Core`:

### 1. `IInputSink` (`src/Mouser.Core/Input/IInputSink.cs`)
Responsible for injecting synthetic mouse and keyboard events into the target OS:
- `Move(int x, int y)` / `MoveRelative(int dx, int dy)`
- `Button(MouseButton button, bool isDown)`
- `Key(ushort virtualKey, bool isDown, char? text)`
- `Wheel(int deltaX, int deltaY)`

### 2. `IInputCapture` (`src/Mouser.Core/Input/IInputCapture.cs`)
Responsible for capturing global low-level mouse and keyboard input on the local system:
- `Start()` and `Stop()`
- `Handler`: delegate receiving `CapturedInput` (must return `true` to swallow the event locally when redirecting to a peer).
- `WarpCursor(int x, int y)`

### 3. `IClipboardAccess` (`src/Mouser.Core/Clipboard/IClipboardAccess.cs`)
Responsible for reading and writing formats (text, HTML, images) from the system clipboard:
- `GetSnapshotAsync()`
- `SetSnapshotAsync(ClipboardSnapshot snapshot)`
- `ClearAsync()`
