'use client';

import React from 'react';
import { motion } from 'framer-motion';
import {
  MousePointer2,
  ClipboardCopy,
  Smartphone,
  Shield,
  Zap,
  FolderSync,
  Layers,
  Sliders,
  Sparkles,
  Lock,
  Globe2,
  Cpu
} from 'lucide-react';

export const FeatureGrid = () => {
  const features = [
    {
      icon: MousePointer2,
      title: 'Seamless Virtual KVM',
      description:
        'Glide your cursor across physical borders of macOS, Windows, and Linux screens. Automatic keyboard focus redirection with native modifier translation.',
      tag: '<2.8ms Latency',
      gradient: 'from-cyan-500/20 to-blue-500/20',
      borderGlow: 'hover:border-cyan-500/50',
    },
    {
      icon: ClipboardCopy,
      title: 'Universal Smart Clipboard',
      description:
        'Real-time clipboard mirroring across all devices. Syncs rich text, images, code snippets, and files with local SQLite FTS5 search and history management.',
      tag: 'Zero-Knowledge E2EE',
      gradient: 'from-indigo-500/20 to-purple-500/20',
      borderGlow: 'hover:border-indigo-500/50',
    },
    {
      icon: Smartphone,
      title: 'Android Companion & Air Mouse',
      description:
        'Turn your Android smartphone or tablet into a wireless multi-touch trackpad, 3D gyro air-mouse, media controller, and mobile clipboard bridge.',
      tag: 'Companion APK',
      gradient: 'from-fuchsia-500/20 to-pink-500/20',
      borderGlow: 'hover:border-fuchsia-500/50',
    },
    {
      icon: FolderSync,
      title: 'Cross-Screen Drag & Drop',
      description:
        'Pick up a file on your MacBook, drag it past the screen edge, and drop it directly onto your Windows desktop via chunked UDP packet streaming.',
      tag: 'Direct P2P Transfer',
      gradient: 'from-emerald-500/20 to-teal-500/20',
      borderGlow: 'hover:border-emerald-500/50',
    },
    {
      icon: Zap,
      title: '1,000 Hz Mouse Smoothing',
      description:
        'Adaptive jitter-buffer algorithms deliver buttery-smooth 1000Hz gaming mouse precision without cursor teleportation or network jitter drops.',
      tag: 'Zero-Alloc UDP',
      gradient: 'from-amber-500/20 to-orange-500/20',
      borderGlow: 'hover:border-amber-500/50',
    },
    {
      icon: Lock,
      title: 'Privacy & Password Firewall',
      description:
        'Sensitive content masking automatically detects and ignores passwords copied from 1Password, Bitwarden, KeePass, and API credential managers.',
      tag: 'Strict Privacy',
      gradient: 'from-rose-500/20 to-red-500/20',
      borderGlow: 'hover:border-rose-500/50',
    },
  ];

  return (
    <section id="features" className="py-24 relative bg-slate-950">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/20 text-cyan-400 text-xs font-semibold uppercase tracking-wider mb-3">
            <Sparkles className="w-3.5 h-3.5" />
            Cutting-Edge Systems Engineering
          </div>
          <h2 className="text-3xl sm:text-5xl font-extrabold text-white tracking-tight">
            Designed for Power Users & Multi-OS Workstations
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-300">
            Engineered from the ground up with low-level C++ OS hooks and high-performance .NET MVVM for speed, security, and developer ergonomics.
          </p>
        </div>

        {/* Feature Grid Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-8">
          {features.map((feat, idx) => {
            const Icon = feat.icon;
            return (
              <motion.div
                key={idx}
                initial={{ opacity: 0, y: 20 }}
                whileInView={{ opacity: 1, y: 0 }}
                viewport={{ once: true }}
                transition={{ duration: 0.5, delay: idx * 0.1 }}
                className={`relative rounded-2xl bg-slate-900/60 border border-slate-800 p-8 shadow-xl backdrop-blur-md transition-all duration-300 hover:-translate-y-1 ${feat.borderGlow} group`}
              >
                {/* Subtle gradient background on hover */}
                <div
                  className={`absolute inset-0 rounded-2xl bg-gradient-to-br ${feat.gradient} opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none`}
                />

                <div className="relative z-10 flex flex-col h-full justify-between">
                  <div>
                    <div className="flex items-center justify-between mb-5">
                      <div className="p-3 rounded-xl bg-slate-800/80 border border-slate-700 text-cyan-400 group-hover:scale-110 transition-transform">
                        <Icon className="w-6 h-6" />
                      </div>
                      <span className="text-[11px] font-mono font-semibold px-2.5 py-1 rounded-full bg-slate-800/80 text-slate-300 border border-slate-700">
                        {feat.tag}
                      </span>
                    </div>

                    <h3 className="text-xl font-bold text-white group-hover:text-cyan-300 transition-colors">
                      {feat.title}
                    </h3>
                    <p className="mt-3 text-slate-400 text-sm leading-relaxed">
                      {feat.description}
                    </p>
                  </div>
                </div>
              </motion.div>
            );
          })}
        </div>
      </div>
    </section>
  );
};
