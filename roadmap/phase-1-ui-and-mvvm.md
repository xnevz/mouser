# Phase 1: Modern UI & MVVM Presentation Layer

> **Objective**: Build a modern, simplistic, professional UI following the macOS aesthetic (deep dark palette, subtle borders, frosted glass translucency, clean typography) using the **XAML / WPF paradigm with Avalonia UI** and **`CommunityToolkit.Mvvm`**.

---

## 1. UI Design Language & Philosophy

- **Color Palette**: Minimalist zinc/slate dark theme (`#18181b` base background, `#27272a` cards, `#3f3f46` borders).
- **Typography**: Native platform font stack (San Francisco on macOS, Segoe UI on Windows, Inter on Linux).
- **Geometry**: Consistent `8px` to `12px` rounded corners, `1px` translucent borders (`rgba(255, 255, 255, 0.08)`).
- **Responsiveness**: Hardware-accelerated animations (Metal on macOS, DirectX on Windows, Vulkan/Skia on Linux).

---

## 2. Key Views & Screens

### 2.1 Main Shell Window (`MainWindow.axaml`)
- **Navigation Sidebar**: Compact vertical panel containing navigation tabs (Display Matrix, Smart Clipboard, Android Remote, Settings).
- **Status Indicator Pill**: Displays live status (e.g., `● Active: 3 Screens Connected`, `● Paused`, or `○ Searching for Peers`).
- **System Tray Integration**: Native tray icon with quick actions (Toggle Sharing, Lock Cursor, Open Clipboard HUD, Exit).

### 2.2 Interactive Monitor Layout Matrix (`MonitorLayoutView.axaml`)
The visual heart of the KVM switch:
- **Visual Desktop Representation**: Drag-and-drop canvas showing cards representing each connected computer's monitors (e.g., "MacBook Pro Retina", "Windows 4K Gaming PC", "Linux Workstation").
- **Magnetic Edge Snapping**: Screens snap cleanly to adjacent display edges with visual magnetic alignment guides.
- **Resolution & DPI Scaling**: Proportional scaling so a 4K display visually matches its relative physical size next to a 1080p display.
- **Active Border Indicator**: Glowing border line showing which edge triggers the cursor transition to the neighbor screen.

### 2.3 Smart Clipboard History HUD (`ClipboardHistoryWindow.axaml`)
- **Floating Semi-Transparent Palette**: Global hotkey (`Cmd+Shift+V` / `Win+V`) summons a spotlight-style floating window centered on the active screen.
- **Instant Search Bar**: Filter historical clips by text, timestamp, or content type (Text, Code, Link, Image).
- **Card Preview**: Formatted preview of selected clip with 1-click or Enter to paste into current active application.

### 2.4 Android Remote Controller View (`AndroidRemoteView.axaml`)
- Status card showing connected phone details (model, battery level, Wi-Fi latency ping in milliseconds).
- Sensitivity sliders for trackpad cursor acceleration, scroll speed, and gyro deadzone.
- Button layout mapping for mobile macro buttons.

---

## 3. MVVM Architecture with `CommunityToolkit.Mvvm`

### 3.1 Project Structure for UI
```
Mouser.UI/
├── App.axaml                      # Application styling and global resources
├── Styles/
│   ├── ThemeColors.axaml          # Zinc dark color palette definitions
│   └── ControlStyles.axaml        # Reusable buttons, cards, sliders, pills
├── Views/
│   ├── MainWindow.axaml
│   ├── MonitorLayoutView.axaml
│   ├── ClipboardHistoryWindow.axaml
│   └── SettingsView.axaml
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── MonitorLayoutViewModel.cs
│   ├── ScreenNodeViewModel.cs
│   └── ClipboardHistoryViewModel.cs
└── Services/                      # UI navigation and window management
    └── IWindowManager.cs
```

### 3.2 ViewModel Implementation Example: `MonitorLayoutViewModel.cs`
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace Mouser.UI.ViewModels;

public partial class MonitorLayoutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "Connected to 2 devices";

    [ObservableProperty]
    private bool _isSharingActive = true;

    [ObservableProperty]
    private ScreenNodeViewModel? _selectedScreen;

    public ObservableCollection<ScreenNodeViewModel> Screens { get; } = new();

    public MonitorLayoutViewModel()
    {
        // Mock data for UI development and preview
        Screens.Add(new ScreenNodeViewModel { Hostname = "MacBook Pro 16\"", Resolution = "3456 × 2234", PositionX = 100, PositionY = 150, IsLocal = true });
        Screens.Add(new ScreenNodeViewModel { Hostname = "Windows PC", Resolution = "3840 × 2160", PositionX = 350, PositionY = 150, IsLocal = false });
    }

    [RelayCommand]
    private void ToggleSharing()
    {
        IsSharingActive = !IsSharingActive;
        StatusText = IsSharingActive ? "KVM Active" : "KVM Paused";
    }

    [RelayCommand]
    private void AutoArrangeScreens()
    {
        // Align screens horizontally based on reported display geometry
        double currentX = 50;
        foreach (var screen in Screens)
        {
            screen.PositionX = currentX;
            currentX += screen.DisplayWidth + 20;
        }
    }
}
```

### 3.3 Visual Screen Card ViewModel: `ScreenNodeViewModel.cs`
```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mouser.UI.ViewModels;

public partial class ScreenNodeViewModel : ObservableObject
{
    [ObservableProperty] private string _hostname = string.Empty;
    [ObservableProperty] private string _resolution = string.Empty;
    [ObservableProperty] private double _positionX;
    [ObservableProperty] private double _positionY;
    [ObservableProperty] private double _displayWidth = 220;
    [ObservableProperty] private double _displayHeight = 140;
    [ObservableProperty] private bool _isLocal;
    [ObservableProperty] private bool _isSelected;
}
```

---

## 4. Phase 1 Implementation Steps

- [ ] **Step 1.1: Project Creation & Avalonia Setup**
  - Run `dotnet new avalonia.app -o Mouser.UI`.
  - Install NuGet packages:
    - `CommunityToolkit.Mvvm` (8.3+)
    - `Microsoft.Extensions.DependencyInjection`
    - `Avalonia.Themes.Fluent`
- [ ] **Step 1.2: Design System & Styling**
  - Define zinc dark palette and typography in `ThemeColors.axaml`.
  - Create reusable custom styles for cards, pills, icon buttons, and sliders.
- [ ] **Step 1.3: Build Main Shell & Navigation**
  - Implement `MainWindow.axaml` with responsive sidebar navigation and live status header.
  - Implement system tray icon with context menu.
- [ ] **Step 1.4: Build Interactive Monitor Canvas**
  - Create `MonitorLayoutView.axaml` using an Avalonia `Canvas` supporting dragging of screen cards.
  - Implement magnetic snapping logic in `MonitorLayoutViewModel`.
- [ ] **Step 1.5: Build Floating Clipboard HUD**
  - Create `ClipboardHistoryWindow.axaml` as a borderless, rounded floating window with search input and keyboard navigation.

---

## 5. Phase 1 Deliverables & Exit Criteria

- [ ] Full modern UI compiles and runs on macOS (`dotnet run --project Mouser.UI`).
- [ ] Drag-and-drop screen canvas is interactive and smoothly updates ViewModel coordinates.
- [ ] Floating clipboard window opens and closes smoothly.
- [ ] All views adhere strictly to the MVVM pattern with zero logic in code-behind files.
