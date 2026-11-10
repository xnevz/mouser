'use client';

import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Cpu,
  Layers,
  Network,
  Smartphone,
  CheckCircle2,
  Code2,
  Terminal,
} from 'lucide-react';
import { StretchyPillTabs } from '@/components/StretchyPillTabs';

export const ArchitecturePresentation = () => {
  const [activeTab, setActiveTab] = useState<'mvvm' | 'native' | 'net' | 'mobile'>('mvvm');

  const architectureLayers = {
    mvvm: {
      title: 'Modern MVVM & Presentation Layer',
      subtitle: '.NET 8/9 with CommunityToolkit.Mvvm & Avalonia XAML UI',
      icon: Layers,
      color: 'text-cyan-400',
      badge: 'C# 12 / Avalonia',
      points: [
        'Pure separation of concerns: Zero business logic in code-behind XAML views.',
        'Interactive Canvas: Drag-and-drop monitor matrix arranging multi-monitor topologies with DPI normalization.',
        'Dark theme by default with responsive real-time telemetry gauges.',
        'Dependency injection via Microsoft.Extensions.DependencyInjection.',
      ],
      codeSnippet: `// MainWindowViewModel.cs
[ObservableProperty]
private MonitorTopology _topology;

[RelayCommand]
private async Task OnCursorCrossBorderAsync(BorderDirection direction)
{
    await _inputRouter.HopToAdjacentScreenAsync(direction);
}`,
    },
    native: {
      title: 'Native C++ System Interop Engine',
      subtitle: 'Zero-Overhead [LibraryImport] P/Invoke with C-ABI Dynamic Libraries',
      icon: Cpu,
      color: 'text-indigo-400',
      badge: 'C++20 / C-ABI',
      points: [
        'macOS: CGEventTap low-level input interception & TCC Accessibility security sandbox.',
        'Windows: Win32 RawInput & SetWindowsHookEx(WH_MOUSE_LL, WH_KEYBOARD_LL).',
        'Linux: Native /dev/uinput virtual device injection & libinput Wayland portal hooks.',
        'Compiled directly via Xcode Clang, MSVC, and GCC into lightweight native dylibs.',
      ],
      codeSnippet: `// NativeMethods.cs
[LibraryImport("libmouser_native", EntryPoint = "mouser_install_event_tap")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
internal static partial int InstallEventTap(
    EventCallback callback,
    nint userData
);`,
    },
    net: {
      title: 'Zero-Allocation UDP Network Pipeline',
      subtitle: '1,000 Hz Sub-Millisecond Input Streaming & mDNS Peer Discovery',
      icon: Network,
      color: 'text-fuchsia-400',
      badge: 'UDP / MemoryPool',
      points: [
        'Zero GC Pauses: Byte manipulation executed via ReadOnlySpan<byte> and ArrayPool<byte>.',
        'AES-256-GCM authenticated cipher streams with automatic rotating session keys.',
        'Zero-Configuration discovery: Automatic peer detection via Zeroconf / mDNS with QR code PIN pairing.',
        'Jitter buffer with packet loss concealment for high-polling gaming mice.',
      ],
      codeSnippet: `// InputPacketSocket.cs
public void ProcessInboundPacket(ReadOnlySpan<byte> packet)
{
    var header = MemoryMarshal.Read<PacketHeader>(packet[..16]);
    if (header.Magic != MouserMagic) return;

    _decryptor.DecryptInPlace(packet[16..]);
    _inputInjector.InjectCursorMove(header.DeltaX, header.DeltaY);
}`,
    },
    mobile: {
      title: 'Android Mobile Companion Subsystem',
      subtitle: 'High-Performance Kotlin, Jetpack Compose & 3D Sensor Fusion Hub',
      icon: Smartphone,
      color: 'text-emerald-400',
      badge: 'Kotlin / Compose',
      points: [
        'Multi-touch virtual trackpad with two-finger scrolling and three-finger app gestures.',
        '3D Gyroscope Air Mouse: Hold trigger button to steer cursor through real-world spatial orientation.',
        'Encrypted mobile clipboard synchronization bridge.',
        'Zero cloud reliance: Direct peer-to-peer local Wi-Fi UDP socket communication.',
      ],
      codeSnippet: `// GyroSensorService.kt
override fun onSensorChanged(event: SensorEvent?) {
    val gyroX = event?.values?.get(0) ?: 0f
    val gyroY = event?.values?.get(1) ?: 0f
    udpClient.sendMotionPacket(dx = -gyroY * sensitivity, dy = -gyroX * sensitivity)
}`,
    },
  };

  const current = architectureLayers[activeTab];

  return (
    <section id="architecture" className="py-24 relative bg-slate-50/60 dark:bg-slate-900/40 border-y border-slate-200 dark:border-slate-800/80">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/20 text-cyan-600 dark:text-cyan-400 text-xs font-semibold uppercase tracking-wider mb-3">
            <Code2 className="w-3.5 h-3.5" />
            Under The Hood
          </div>
          <h2 className="text-3xl sm:text-5xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            Engineered for Raw Performance
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
            A cohesive architecture combining managed .NET MVVM safety with uncompromised native C++ operating system hooks.
          </p>
        </div>

        {/* Stretchy Elastic Tab Buttons */}
        <div className="flex justify-center mb-12">
          <StretchyPillTabs
            tabs={[
              { id: 'mvvm', label: '1. UI & MVVM Layer', icon: Layers },
              { id: 'native', label: '2. Native C++ Drivers', icon: Cpu },
              { id: 'net', label: '3. Zero-Alloc UDP', icon: Network },
              { id: 'mobile', label: '4. Android Mobile', icon: Smartphone },
            ]}
            activeId={activeTab}
            onChange={(id) => setActiveTab(id as 'mvvm' | 'native' | 'net' | 'mobile')}
            pillColor="from-cyan-500 via-indigo-500 to-blue-600"
            layoutId="architecture-active-pill"
          />
        </div>

        {/* Tab Content Presentation */}
        <AnimatePresence mode="wait">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 16, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -16, scale: 0.98 }}
            transition={{ type: 'spring', stiffness: 350, damping: 25 }}
            className="rounded-3xl bg-white/95 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 p-6 sm:p-10 shadow-xl dark:shadow-2xl"
          >
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-8 items-center">
              {/* Left: Architecture Points */}
              <div>
                <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-slate-100 dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-xs font-mono text-cyan-600 dark:text-cyan-400 mb-4">
                  <span>{current.badge}</span>
                </div>
                <h3 className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white">{current.title}</h3>
                <p className="mt-2 text-slate-600 dark:text-slate-400 text-sm sm:text-base leading-relaxed">
                  {current.subtitle}
                </p>

                <div className="mt-6 space-y-3">
                  {current.points.map((pt, idx) => (
                    <div key={idx} className="flex items-start gap-3">
                      <CheckCircle2 className="w-5 h-5 text-cyan-500 dark:text-cyan-400 flex-shrink-0 mt-0.5" />
                      <span className="text-sm text-slate-700 dark:text-slate-300 leading-normal">{pt}</span>
                    </div>
                  ))}
                </div>
              </div>

              {/* Right: Code Snippet Card */}
              <div className="rounded-2xl bg-slate-900 dark:bg-slate-900/90 border border-slate-800 p-4 sm:p-6 font-mono text-xs shadow-inner">
                <div className="flex items-center justify-between pb-3 mb-3 border-b border-slate-800 text-slate-400">
                  <div className="flex items-center gap-2">
                    <Terminal className="w-4 h-4 text-cyan-400" />
                    <span className="text-[11px]">Source Architecture Preview</span>
                  </div>
                  <span className="text-[10px] px-2 py-0.5 rounded bg-slate-800 text-slate-400">
                    Read Only
                  </span>
                </div>
                <pre className="overflow-x-auto text-slate-300 leading-relaxed font-mono text-xs sm:text-[13px]">
                  <code>{current.codeSnippet}</code>
                </pre>
              </div>
            </div>
          </motion.div>
        </AnimatePresence>
      </div>
    </section>
  );
};
