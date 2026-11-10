'use client';

import React, { useState, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  MousePointer2,
  Laptop,
  Monitor,
  Smartphone,
  Shield,
  Activity,
  Play,
  Pause,
  Copy,
  CheckCheck
} from 'lucide-react';

interface ScreenNode {
  id: string;
  name: string;
  os: string;
  resolution: string;
  ip: string;
  icon: typeof Laptop;
  color: string;
  borderColor: string;
}

export const InteractiveCanvasDemo = () => {
  const [activeScreenIndex, setActiveScreenIndex] = useState(0);
  const [isPlaying, setIsPlaying] = useState(true);
  const [packetsCount, setPacketsCount] = useState(14820);
  const copiedItem = 'git clone https://github.com/mouser-org/mouser';
  const [showClipboardToast, setShowClipboardToast] = useState(false);

  const screens: ScreenNode[] = [
    {
      id: 'mac',
      name: 'MacBook Pro 16"',
      os: 'macOS Sequoia 15.1',
      resolution: '3456 × 2234 (Retina)',
      ip: '192.168.1.42',
      icon: Laptop,
      color: 'from-cyan-500/20 to-blue-600/10',
      borderColor: 'border-cyan-500/40',
    },
    {
      id: 'win',
      name: 'Custom Desktop PC',
      os: 'Windows 11 Pro',
      resolution: '3840 × 2160 (4K 144Hz)',
      ip: '192.168.1.88',
      icon: Monitor,
      color: 'from-indigo-500/20 to-purple-600/10',
      borderColor: 'border-indigo-500/40',
    },
    {
      id: 'linux',
      name: 'Dev Station',
      os: 'Ubuntu 24.04 (Wayland)',
      resolution: '2560 × 1440 (QHD)',
      ip: '192.168.1.105',
      icon: Monitor,
      color: 'from-amber-500/20 to-orange-600/10',
      borderColor: 'border-amber-500/40',
    },
  ];

  // Automated roaming simulation
  useEffect(() => {
    if (!isPlaying) return;

    const interval = setInterval(() => {
      setActiveScreenIndex((prev) => (prev + 1) % screens.length);
      setPacketsCount((prev) => prev + Math.floor(Math.random() * 24 + 18));
    }, 2800);

    return () => clearInterval(interval);
  }, [isPlaying, screens.length]);

  const triggerClipboardSync = () => {
    setShowClipboardToast(true);
    setTimeout(() => setShowClipboardToast(false), 2400);
  };

  return (
    <section id="demo" className="py-20 relative bg-slate-100/60 dark:bg-slate-950/60 border-y border-slate-200 dark:border-slate-800/80">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Section Header */}
        <div className="text-center max-w-3xl mx-auto mb-12">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/20 text-cyan-600 dark:text-cyan-400 text-xs font-semibold uppercase tracking-wider mb-3">
            <Activity className="w-3.5 h-3.5" />
            Live Hardware Simulation
          </div>
          <h2 className="text-3xl sm:text-5xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            How Mouser Works in Real Time
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
            Slide mouse cursor past the border of any screen. Mouser captures raw peripheral events, streams zero-copy UDP packets to adjacent peer machines, and synchronizes the system clipboard across platforms.
          </p>
        </div>

        {/* Live Simulation Canvas */}
        <div className="relative rounded-2xl bg-white/95 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 p-6 sm:p-8 shadow-xl dark:shadow-2xl backdrop-blur-xl overflow-hidden">
          {/* Subtle glow background */}
          <div className="absolute -top-24 -left-24 w-96 h-96 bg-cyan-500/10 rounded-full blur-[100px] pointer-events-none" />
          <div className="absolute -bottom-24 -right-24 w-96 h-96 bg-indigo-500/10 rounded-full blur-[100px] pointer-events-none" />

          {/* Top Control Bar & Live Diagnostics */}
          <div className="flex flex-wrap items-center justify-between gap-4 pb-6 mb-8 border-b border-slate-200 dark:border-slate-800 text-xs sm:text-sm">
            <div className="flex items-center gap-3">
              <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 font-mono text-slate-700 dark:text-slate-300">
                <span className="w-2 h-2 rounded-full bg-emerald-500 dark:bg-emerald-400 animate-pulse" />
                <span>Link: 1,000 Hz UDP Socket</span>
              </div>
              <div className="hidden sm:flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 font-mono text-cyan-700 dark:text-cyan-400">
                <Activity className="w-3.5 h-3.5" />
                <span>Packets: {packetsCount.toLocaleString()}</span>
              </div>
              <div className="hidden md:flex items-center gap-2 px-3 py-1.5 rounded-lg bg-slate-100 dark:bg-slate-800/80 border border-slate-200 dark:border-slate-700 font-mono text-fuchsia-700 dark:text-fuchsia-400">
                <Shield className="w-3.5 h-3.5" />
                <span>E2EE: AES-256-GCM Active</span>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => setIsPlaying(!isPlaying)}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 dark:bg-slate-800 dark:hover:bg-slate-700 text-slate-700 dark:text-slate-200 border border-slate-300 dark:border-slate-600 transition-colors font-medium"
              >
                {isPlaying ? (
                  <>
                    <Pause className="w-3.5 h-3.5 text-amber-500 dark:text-amber-400" />
                    <span>Pause Tour</span>
                  </>
                ) : (
                  <>
                    <Play className="w-3.5 h-3.5 text-emerald-500 dark:text-emerald-400" />
                    <span>Resume Tour</span>
                  </>
                )}
              </button>

              <button
                onClick={triggerClipboardSync}
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-cyan-500/10 hover:bg-cyan-500/20 text-cyan-700 dark:text-cyan-300 border border-cyan-500/30 transition-colors font-medium"
              >
                <Copy className="w-3.5 h-3.5" />
                <span>Simulate Clipboard Copy</span>
              </button>
            </div>
          </div>

          {/* Multi-Monitor Visual Grid */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-6 relative">
            {screens.map((screen, idx) => {
              const isActive = activeScreenIndex === idx;
              const IconComponent = screen.icon;

              return (
                <div
                  key={screen.id}
                  onClick={() => setActiveScreenIndex(idx)}
                  className={`relative cursor-pointer rounded-xl transition-all duration-300 border p-4 sm:p-5 flex flex-col justify-between min-h-[260px] ${
                    isActive
                      ? `bg-white dark:bg-slate-900 shadow-xl shadow-cyan-500/10 ${screen.borderColor} ring-1 ring-cyan-500/50`
                      : 'bg-slate-50 dark:bg-slate-950/70 border-slate-200 dark:border-slate-800/80 hover:border-slate-300 dark:hover:border-slate-700'
                  }`}
                >
                  {/* Active Screen Indicator Badge */}
                  <div className="flex items-center justify-between mb-4">
                    <div className="flex items-center gap-2">
                      <IconComponent className={`w-5 h-5 ${isActive ? 'text-cyan-600 dark:text-cyan-400' : 'text-slate-400'}`} />
                      <span className="font-bold text-slate-900 dark:text-white text-sm sm:text-base">{screen.name}</span>
                    </div>
                    {isActive ? (
                      <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-cyan-500/20 text-cyan-600 dark:text-cyan-400 border border-cyan-500/40 animate-pulse">
                        CURSOR ACTIVE
                      </span>
                    ) : (
                      <span className="text-[11px] font-mono text-slate-400 dark:text-slate-500">Standby</span>
                    )}
                  </div>

                  {/* Simulated Desktop Window */}
                  <div className="relative rounded-lg bg-slate-100 dark:bg-slate-950/90 border border-slate-200 dark:border-slate-800 p-3 h-32 flex flex-col justify-between overflow-hidden">
                    <div className="flex items-center justify-between pb-2 border-b border-slate-200 dark:border-slate-800/60">
                      <div className="flex gap-1.5">
                        <span className="w-2 h-2 rounded-full bg-red-500/70" />
                        <span className="w-2 h-2 rounded-full bg-yellow-500/70" />
                        <span className="w-2 h-2 rounded-full bg-green-500/70" />
                      </div>
                      <span className="text-[10px] font-mono text-slate-500">{screen.os}</span>
                    </div>

                    {/* Window content simulation */}
                    <div className="flex-1 flex items-center justify-center relative">
                      {isActive ? (
                        <motion.div
                          animate={{
                            x: [0, 40, -30, 20, 0],
                            y: [0, -15, 10, -5, 0],
                          }}
                          transition={{
                            repeat: Infinity,
                            duration: 2.2,
                            ease: 'easeInOut',
                          }}
                          className="flex items-center gap-1.5 px-2.5 py-1 rounded bg-cyan-500/20 border border-cyan-400/40 text-cyan-700 dark:text-cyan-300 shadow-md text-xs font-mono"
                        >
                          <MousePointer2 className="w-3.5 h-3.5 text-cyan-600 dark:text-cyan-400 fill-cyan-400" />
                          <span>Input Captured (0.8ms)</span>
                        </motion.div>
                      ) : (
                        <div className="text-center text-slate-400 dark:text-slate-600 text-xs font-mono">
                          Boundary ready for transition →
                        </div>
                      )}
                    </div>

                    {/* Taskbar / Dock indicator */}
                    <div className="flex justify-center gap-2 pt-1.5 border-t border-slate-200 dark:border-slate-800/60">
                      <div className="w-4 h-1.5 rounded-sm bg-slate-300 dark:bg-slate-700" />
                      <div className="w-4 h-1.5 rounded-sm bg-slate-300 dark:bg-slate-700" />
                      <div className="w-4 h-1.5 rounded-sm bg-cyan-500/80" />
                      <div className="w-4 h-1.5 rounded-sm bg-slate-300 dark:bg-slate-700" />
                    </div>
                  </div>

                  {/* Metadata footer */}
                  <div className="mt-4 pt-3 border-t border-slate-200 dark:border-slate-800/80 flex items-center justify-between text-[11px] font-mono text-slate-500 dark:text-slate-400">
                    <span>{screen.resolution}</span>
                    <span>{screen.ip}</span>
                  </div>
                </div>
              );
            })}
          </div>

          {/* Android Companion Air Mouse Ribbon */}
          <div className="mt-6 p-4 rounded-xl bg-gradient-to-r from-slate-50 via-indigo-50/40 to-slate-50 dark:from-slate-900 dark:via-indigo-950/40 dark:to-slate-900 border border-indigo-200 dark:border-indigo-500/30 flex flex-col sm:flex-row items-center justify-between gap-4">
            <div className="flex items-center gap-3">
              <div className="p-2.5 rounded-lg bg-indigo-500/10 dark:bg-indigo-500/20 text-indigo-600 dark:text-indigo-400 border border-indigo-500/20 dark:border-indigo-500/30">
                <Smartphone className="w-5 h-5" />
              </div>
              <div>
                <div className="font-semibold text-slate-900 dark:text-white text-sm flex items-center gap-2">
                  <span>Android Companion Hub & 3D Gyro Air Mouse</span>
                  <span className="px-2 py-0.5 rounded bg-indigo-500/10 dark:bg-indigo-500/20 text-indigo-600 dark:text-indigo-300 text-[10px] font-mono">
                    Connected
                  </span>
                </div>
                <p className="text-xs text-slate-600 dark:text-slate-400">
                  Transform any Android phone into a multi-touch trackpad, gyro presenter, and encrypted clipboard bridge.
                </p>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <a
                href="#downloads"
                className="px-3.5 py-1.5 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-semibold transition-colors flex items-center gap-1.5"
              >
                <span>Get Companion APK</span>
              </a>
            </div>
          </div>
        </div>

        {/* Real-time Clipboard Synchronization Toast */}
        <AnimatePresence>
          {showClipboardToast && (
            <motion.div
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: -20 }}
              className="fixed bottom-8 right-8 z-50 p-4 rounded-xl bg-slate-900 border border-cyan-500/50 shadow-2xl shadow-cyan-500/20 max-w-sm"
            >
              <div className="flex items-start gap-3">
                <div className="p-2 rounded-lg bg-cyan-500/20 text-cyan-400">
                  <CheckCheck className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-xs font-bold text-white flex items-center gap-1.5">
                    <span>Clipboard Synchronized Instantly</span>
                  </div>
                  <p className="text-[11px] text-slate-300 mt-1 font-mono break-all bg-slate-950 p-1.5 rounded border border-slate-800">
                    {copiedItem}
                  </p>
                  <span className="text-[10px] text-cyan-400 mt-1 block">
                    Synced across macOS, Windows, Linux & Android in 1.4ms
                  </span>
                </div>
              </div>
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    </section>
  );
};
