# Mouser: Engineering Roadmap & Implementation Phases

Welcome to the phased implementation plan for **Mouser** — the cross-platform Virtual KVM and Universal Smart Clipboard system built with **.NET 8/9 (C#)**, **MVVM**, and **low-level C/C++ native drivers**.

Each file in this directory represents a distinct phase containing architecture blueprints, prerequisites, implementation steps, testing guidelines, and deliverables.

---

## Roadmap Navigation

| Phase | Title | Focus Area | Status |
| :---: | :--- | :--- | :---: |
| **[Phase 0](./phase-0-macos-dev-setup.md)** | **macOS Development Setup & Prerequisites** | Toolchains, SDKs, Xcode Clang, TCC Security, dylib build pipeline | **Complete** |
| **[Phase 1](./phase-1-ui-and-mvvm.md)** | **Modern UI & MVVM Presentation Layer** | XAML/Avalonia UI, Dark Theme, Interactive Monitor Canvas, ViewModels | **Complete** |
| **[Phase 2](./phase-2-native-interop-and-networking.md)** | **Native C++ Interop & Networking Engine** | `[LibraryImport]`, C-ABI, Zero-Alloc UDP Sockets, mDNS Discovery | **Complete** |
| **[Phase 3](./phase-3-android-companion.md)** | **Android Companion & Remote Input Hub** | Touchpad, 3D Gyro Air Mouse, Android UDP client, Desktop injection | Complete |
| **[Phase 4](./phase-4-virtual-kvm-engine.md)** | **Virtual KVM & Mouse/Keyboard Sharing** | `CGEventTap`, Win32 Hooks, `uinput`, Edge Hopping, Key Translation | Ready |
| **[Phase 5](./phase-5-smart-clipboard-engine.md)** | **Universal Multi-Format Smart Clipboard** | Cross-platform sync, E2EE encryption, SQLite FTS5 history, HUD | Ready |
| **[Phase 6](./phase-6-packaging-and-notarization.md)** | **Hardening, Packaging & Notarization** | Apple Notarization, Windows MSIX, Linux Flatpak, Performance | Ready |
| **[Web Portal Roadmap](../web/roadmap/README.md)** | **Web Showcase & Multi-Platform Download Portal** | Next.js 16, Universal Downloads, KVM Simulator, Vercel/GitHub Pages | **Complete** |

---

## Architectural Principles

1. **Clean Separation of Layers**:
   - **UI Layer**: Pure XAML with `CommunityToolkit.Mvvm` (zero business logic in code-behind).
   - **Service Layer**: Managed C# services registered in `Microsoft.Extensions.DependencyInjection`.
   - **Native Layer**: C/C++ dynamic libraries exposing an `extern "C"` ABI for high-performance OS hooks.
2. **Zero-Allocation Network Pipeline**:
   - High-frequency mouse packets (1,000Hz) process via `ReadOnlySpan<byte>` and memory pools to eliminate GC pauses.
3. **Cross-Platform Parity**:
   - macOS, Windows, Linux, and Android share the identical C# ViewModels and network protocols.
