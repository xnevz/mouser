'use client';

import React from 'react';
import { motion } from 'framer-motion';
import {
  MousePointer2,
  ClipboardCopy,
  Smartphone,
  FolderSync,
  Shield,
  Cpu,
} from 'lucide-react';

export const FeatureGrid = () => {
  return (
    <section id="features" className="py-24 relative">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Section Header */}
        <div className="text-center max-w-3xl mx-auto mb-16">
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-cyan-500/10 border border-cyan-500/30 text-cyan-600 dark:text-cyan-400 text-xs font-semibold uppercase tracking-wider mb-4 shadow-[0_0_15px_rgba(6,182,212,0.15)]">
            <Cpu className="w-3.5 h-3.5" />
            Bespoke Systems Engineering
          </div>
          <h2 className="text-3xl sm:text-5xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            Built for Multi-OS Power Users
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
            Engineered at the native kernel-hook level to eliminate USB switches and cloud subscriptions.
          </p>
        </div>

        {/* Bento Grid Architecture */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
          {/* Card 1: Virtual KVM Zero-Latency (2 Columns) */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.5 }}
            className="md:col-span-2 rounded-3xl bg-white/95 dark:bg-slate-900/85 border border-slate-200 dark:border-slate-800/90 p-7 sm:p-9 shadow-lg dark:shadow-2xl backdrop-blur-xl relative overflow-hidden flex flex-col justify-between group hover:border-cyan-500/40 transition-colors"
          >
            <div>
              <div className="flex items-center justify-between pb-6 border-b border-slate-200 dark:border-slate-800">
                <div className="flex items-center gap-3">
                  <div className="p-3 rounded-2xl bg-cyan-500/10 border border-cyan-500/25 text-cyan-500 dark:text-cyan-400">
                    <MousePointer2 className="w-6 h-6" />
                  </div>
                  <div>
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">Sub-3ms Virtual KVM Switch</h3>
                    <p className="text-xs text-slate-500 dark:text-slate-400 font-mono">RawInput • CGEventTap • uinput</p>
                  </div>
                </div>
                <div className="hidden sm:flex items-center gap-2 px-3 py-1 rounded-full bg-slate-100 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 text-xs font-mono text-cyan-600 dark:text-cyan-400">
                  <span className="w-2 h-2 rounded-full bg-cyan-500 dark:bg-cyan-400 animate-pulse" />
                  <span>1,000 Hz Jitter Buffer</span>
                </div>
              </div>

              <p className="mt-5 text-sm sm:text-base text-slate-600 dark:text-slate-300 leading-relaxed max-w-xl">
                Seamlessly glide past screen edges between macOS, Windows, and Linux. Modifier keys (<code className="text-cyan-700 dark:text-cyan-300 bg-slate-100 dark:bg-slate-950 px-1.5 py-0.5 rounded font-mono">⌘ ↔ ⊞ / Ctrl</code>) translate bidirectionally in microsecond native callbacks without USB switchers or lag.
              </p>
            </div>

            {/* Interactive Telemetry Box */}
            <div className="mt-8 p-4 rounded-2xl bg-slate-50 dark:bg-slate-950/90 border border-slate-200 dark:border-slate-800 font-mono text-xs flex flex-wrap items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase">Input Lag</span>
                  <span className="text-cyan-600 dark:text-cyan-400 font-bold text-sm">2.4 ms</span>
                </div>
                <div className="h-6 w-px bg-slate-200 dark:bg-slate-800" />
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase">Screen Hop Origin</span>
                  <span className="text-slate-800 dark:text-slate-200">MacBook (3456x2234)</span>
                </div>
                <div className="h-6 w-px bg-slate-200 dark:bg-slate-800" />
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase">Destination</span>
                  <span className="text-slate-800 dark:text-slate-200">PC (3840x2160 4K)</span>
                </div>
              </div>
              <span className="text-emerald-700 dark:text-emerald-400 text-[11px] bg-emerald-500/10 px-2.5 py-1 rounded-full border border-emerald-500/20">
                DPI Normalization Active
              </span>
            </div>
          </motion.div>

          {/* Card 2: Universal Smart Clipboard (1 Column) */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.5, delay: 0.1 }}
            className="rounded-3xl bg-white/95 dark:bg-slate-900/85 border border-slate-200 dark:border-slate-800/90 p-7 sm:p-9 shadow-lg dark:shadow-2xl backdrop-blur-xl relative overflow-hidden flex flex-col justify-between group hover:border-indigo-500/40 transition-colors"
          >
            <div>
              <div className="flex items-center justify-between pb-6 border-b border-slate-200 dark:border-slate-800">
                <div className="p-3 rounded-2xl bg-indigo-500/10 border border-indigo-500/25 text-indigo-500 dark:text-indigo-400">
                  <ClipboardCopy className="w-6 h-6" />
                </div>
                <span className="px-2.5 py-1 rounded-full bg-indigo-500/10 text-indigo-700 dark:text-indigo-300 border border-indigo-500/20 text-xs font-mono font-semibold">
                  E2EE Local Sync
                </span>
              </div>

              <h3 className="mt-5 text-xl font-bold text-slate-900 dark:text-white">Smart Clipboard Sync</h3>
              <p className="mt-2 text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
                AES-256-GCM encrypted clipboard mirroring for rich text, PNG images, and code snippets. SQLite FTS5 local history with zero cloud dependencies.
              </p>
            </div>

            <div className="mt-6 p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-950/90 border border-slate-200 dark:border-slate-800 space-y-2">
              <div className="flex items-center justify-between text-xs font-mono text-slate-500 dark:text-slate-400">
                <span className="flex items-center gap-1.5 text-indigo-700 dark:text-indigo-300">
                  <Shield className="w-3.5 h-3.5" /> Password Shield
                </span>
                <span className="text-emerald-600 dark:text-emerald-400">Masked</span>
              </div>
              <p className="text-[11px] text-slate-500 dark:text-slate-400 font-mono leading-normal">
                Ignores 1Password, Bitwarden, KeePass & credit cards automatically.
              </p>
            </div>
          </motion.div>

          {/* Card 3: Android 3D Gyro Hub (1 Column) */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.5, delay: 0.2 }}
            className="rounded-3xl bg-white/95 dark:bg-slate-900/85 border border-slate-200 dark:border-slate-800/90 p-7 sm:p-9 shadow-lg dark:shadow-2xl backdrop-blur-xl relative overflow-hidden flex flex-col justify-between group hover:border-fuchsia-500/40 transition-colors"
          >
            <div>
              <div className="flex items-center justify-between pb-6 border-b border-slate-200 dark:border-slate-800">
                <div className="p-3 rounded-2xl bg-fuchsia-500/10 border border-fuchsia-500/25 text-fuchsia-500 dark:text-fuchsia-400">
                  <Smartphone className="w-6 h-6" />
                </div>
                <span className="px-2.5 py-1 rounded-full bg-fuchsia-500/10 text-fuchsia-700 dark:text-fuchsia-300 border border-fuchsia-500/20 text-xs font-mono font-semibold">
                  Companion APK
                </span>
              </div>

              <h3 className="mt-5 text-xl font-bold text-slate-900 dark:text-white">Android 3D Air Mouse</h3>
              <p className="mt-2 text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
                Transform any phone into a multi-touch trackpad and 3D spatial gyro presenter mouse. Direct UDP socket connection with zero cloud servers.
              </p>
            </div>

            <div className="mt-6 p-3.5 rounded-2xl bg-slate-50 dark:bg-slate-950/90 border border-slate-200 dark:border-slate-800 font-mono text-xs flex items-center justify-between">
              <div>
                <span className="text-[10px] text-slate-500 block uppercase">Sensor Sampling</span>
                <span className="text-fuchsia-700 dark:text-fuchsia-300 font-bold">120 Hz Gyro</span>
              </div>
              <div className="text-right">
                <span className="text-[10px] text-slate-500 block uppercase">Pairing</span>
                <span className="text-slate-800 dark:text-slate-300">mDNS QR PIN</span>
              </div>
            </div>
          </motion.div>

          {/* Card 4: Drag & Drop P2P Streaming (2 Columns) */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.5, delay: 0.3 }}
            className="md:col-span-2 rounded-3xl bg-white/95 dark:bg-slate-900/85 border border-slate-200 dark:border-slate-800/90 p-7 sm:p-9 shadow-lg dark:shadow-2xl backdrop-blur-xl relative overflow-hidden flex flex-col justify-between group hover:border-emerald-500/40 transition-colors"
          >
            <div>
              <div className="flex items-center justify-between pb-6 border-b border-slate-200 dark:border-slate-800">
                <div className="flex items-center gap-3">
                  <div className="p-3 rounded-2xl bg-emerald-500/10 border border-emerald-500/25 text-emerald-500 dark:text-emerald-400">
                    <FolderSync className="w-6 h-6" />
                  </div>
                  <div>
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">Direct Peer-to-Peer Drag & Drop</h3>
                    <p className="text-xs text-slate-500 dark:text-slate-400 font-mono">64KB Chunked Streaming • Zero-Alloc ArrayPool</p>
                  </div>
                </div>
                <span className="px-2.5 py-1 rounded-full bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border border-emerald-500/20 text-xs font-mono font-semibold">
                  Local Network Only
                </span>
              </div>

              <p className="mt-5 text-sm sm:text-base text-slate-600 dark:text-slate-300 leading-relaxed max-w-xl">
                Grab any file or folder from your MacBook desktop, drag it across the monitor border, and drop it on your Windows or Linux PC. Streams at full LAN wire speed with instant SHA-256 integrity verification.
              </p>
            </div>

            <div className="mt-8 p-4 rounded-2xl bg-slate-50 dark:bg-slate-950/90 border border-slate-200 dark:border-slate-800 font-mono text-xs flex flex-wrap items-center justify-between gap-4">
              <div className="flex items-center gap-4">
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase">Memory Overhead</span>
                  <span className="text-emerald-600 dark:text-emerald-400 font-bold">&lt; 28 MB RAM</span>
                </div>
                <div className="h-6 w-px bg-slate-200 dark:bg-slate-800" />
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase">GC Allocation</span>
                  <span className="text-slate-800 dark:text-slate-200">0 B / packet</span>
                </div>
                <div className="h-6 w-px bg-slate-200 dark:bg-slate-800" />
                <div>
                  <span className="text-slate-500 block text-[10px] uppercase">Telemetry</span>
                  <span className="text-slate-800 dark:text-slate-200">100% Offline</span>
                </div>
              </div>
              <span className="text-cyan-600 dark:text-cyan-400 text-[11px] bg-cyan-500/10 px-2.5 py-1 rounded-full border border-cyan-500/20">
                Hardware Acceleration Enabled
              </span>
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
};
