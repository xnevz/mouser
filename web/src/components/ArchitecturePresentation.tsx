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
      title: 'Desktop app: Avalonia + MVVM',
      subtitle: '.NET 8, Avalonia 12 and CommunityToolkit.Mvvm on Windows, macOS and Linux',
      icon: Layers,
      color: 'text-cyan-400',
      badge: 'C# / Avalonia',
      points: [
        'View models hold all logic and are covered by unit tests; views only bind.',
        'Settings are saved as JSON in the user data folder; clipboard history lives in SQLite with full-text search.',
        'The live monitor layout comes straight from the operating system, including scaling.',
      ],
      codeSnippet: `// ClipboardHistoryViewModel.cs
if (_settings.Current.SkipSensitiveClips &&
    ClipboardPrivacyFilter.IsSensitive(text))
{
    StatusMessage = "Skipped a copied secret.";
    return false;
}`,
    },
    native: {
      title: 'Input injection',
      subtitle: 'SendInput on Windows, CGEvent through a small native library on macOS',
      icon: Cpu,
      color: 'text-indigo-400',
      badge: 'Win32 / CoreGraphics',
      points: [
        'Windows uses user32 SendInput directly from C#; no native library to install.',
        'macOS uses libmouser_native (Objective-C++) and needs the Accessibility permission.',
        'Keys travel as Windows virtual-key codes and are translated for macOS.',
        'Windows captures input with low-level hooks; injected events are ignored so nothing loops back.',
        'Linux injection and capture (XTest/uinput) are not written yet.',
      ],
      codeSnippet: `// InputInjectionService.cs: packets carry the full
// button mask, so a lost packet can't leave a button stuck
byte changed = (byte)(newState ^ _buttonState);
foreach (var button in Buttons)
    if ((changed & (byte)button) != 0)
        _sink.SetButton(button, (newState & (byte)button) != 0);`,
    },
    net: {
      title: 'Network protocol',
      subtitle: '14-byte UDP packets, broadcast discovery and PIN pairing',
      icon: Network,
      color: 'text-fuchsia-400',
      badge: 'UDP',
      points: [
        'Desktops broadcast a small beacon every second on each network adapter; phones listen and list them.',
        'A phone pairs by sending the 6-digit PIN; five wrong guesses lock it out for a minute.',
        'Per-sender sequence numbers drop duplicated or reordered packets and survive app restarts.',
        'Pings measure latency and detect a dropped connection; the KVM hands control back if a peer goes quiet.',
        'Clipboard and files travel over TCP, AES-256-GCM encrypted with a key derived from the pairing PIN.',
        'Mouse and keyboard packets are not encrypted: use Mouser on networks you trust.',
      ],
      codeSnippet: `// SequenceTracker.cs
short diff = unchecked((short)(sequence - last));
// late or duplicate packet: drop it.
// A big jump backwards means the phone app restarted.
if (diff <= 0 && diff >= -RestartWindow) return false;`,
    },
    mobile: {
      title: 'Android companion',
      subtitle: 'Kotlin and Jetpack Compose',
      icon: Smartphone,
      color: 'text-emerald-400',
      badge: 'Kotlin / Compose',
      points: [
        'Trackpad with tap, two-finger tap, two-finger scroll and hold-to-drag buttons.',
        'Gyroscope air mouse that only uses the sensor while the trigger is held.',
        'Sub-pixel accumulation so slow movements still move the pointer.',
        'Remembers the last computer and its PIN, and reconnects on launch.',
      ],
      codeSnippet: `// Protocol.kt
fun add(delta: Float): Int {
    val total = remainder + delta
    val whole = total.toInt()
    remainder = total - whole
    return whole
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
            How it works
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
            A .NET desktop app, a small native library for macOS, and a Kotlin phone app talking over a simple UDP protocol.
          </p>
        </div>

        {/* Stretchy Elastic Tab Buttons */}
        <div className="flex justify-center mb-12">
          <StretchyPillTabs
            tabs={[
              { id: 'mvvm', label: 'Desktop app', icon: Layers },
              { id: 'native', label: 'Input injection', icon: Cpu },
              { id: 'net', label: 'Network', icon: Network },
              { id: 'mobile', label: 'Android', icon: Smartphone },
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
              <div className="rounded-2xl bg-slate-100/90 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 p-4 sm:p-6 font-mono text-xs shadow-sm dark:shadow-inner transition-colors">
                <div className="flex items-center justify-between pb-3 mb-3 border-b border-slate-200 dark:border-slate-800 text-slate-500 dark:text-slate-400">
                  <div className="flex items-center gap-2">
                    <Terminal className="w-4 h-4 text-cyan-600 dark:text-cyan-400" />
                    <span className="text-[11px] font-semibold text-slate-700 dark:text-slate-300">
                      Source Architecture Preview
                    </span>
                  </div>
                  <span className="text-[10px] px-2 py-0.5 rounded bg-slate-200 dark:bg-slate-800 text-slate-600 dark:text-slate-400 font-medium">
                    Read Only
                  </span>
                </div>
                <pre className="overflow-x-auto text-slate-800 dark:text-slate-200 leading-relaxed font-mono text-xs sm:text-[13px]">
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
