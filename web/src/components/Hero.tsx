'use client';

import React, { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import {
  Download,
  Terminal,
  ShieldCheck,
  Zap,
  Layers,
  Copy,
  Check,
  Sparkles,
  ChevronRight,
  ArrowRight,
  Apple,
  Laptop,
  Smartphone
} from 'lucide-react';
import { PLATFORM_RELEASES, CURRENT_RELEASE_VERSION } from '@/data/releases';
import { PlatformId } from '@/types/download';

export const Hero = () => {
  const [detectedOs, setDetectedOs] = useState<PlatformId>('macos');
  const [detectedArch, setDetectedArch] = useState<string>('arm64');
  const [copiedCmd, setCopiedCmd] = useState<string | null>(null);
  const [selectedCliTab, setSelectedCliTab] = useState<'brew' | 'winget' | 'flatpak'>('brew');

  useEffect(() => {
    // Client-side OS and Architecture detection
    if (typeof window !== 'undefined') {
      const userAgent = window.navigator.userAgent.toLowerCase();
      if (userAgent.includes('mac')) {
        setDetectedOs('macos');
        // Check Apple Silicon heuristic
        setDetectedArch('arm64');
        setSelectedCliTab('brew');
      } else if (userAgent.includes('win')) {
        setDetectedOs('windows');
        setDetectedArch(userAgent.includes('arm') ? 'arm64' : 'x64');
        setSelectedCliTab('winget');
      } else if (userAgent.includes('android')) {
        setDetectedOs('android');
        setDetectedArch('apk');
      } else if (userAgent.includes('linux')) {
        setDetectedOs('linux');
        setDetectedArch('x64');
        setSelectedCliTab('flatpak');
      } else if (userAgent.includes('iphone') || userAgent.includes('ipad')) {
        setDetectedOs('ios');
        setDetectedArch('universal');
      }
    }
  }, []);

  const currentPlatform = PLATFORM_RELEASES[detectedOs] || PLATFORM_RELEASES.macos;
  const primaryDownload = currentPlatform.primaryArtifact;

  const copyToClipboard = (text: string, id: string) => {
    navigator.clipboard.writeText(text);
    setCopiedCmd(id);
    setTimeout(() => setCopiedCmd(null), 2500);
  };

  const cliCommands = {
    brew: {
      label: 'macOS (Homebrew)',
      cmd: 'brew install --cask mouser',
      id: 'brew',
    },
    winget: {
      label: 'Windows (Winget)',
      cmd: 'winget install Mouser.Mouser',
      id: 'winget',
    },
    flatpak: {
      label: 'Linux (Flathub)',
      cmd: 'flatpak install flathub org.mouser.Mouser',
      id: 'flatpak',
    },
  };

  return (
    <section className="relative pt-28 pb-20 md:pt-36 md:pb-32 overflow-hidden bg-radial-gradient">
      {/* Decorative backdrop glow spheres */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[700px] h-[400px] bg-gradient-to-tr from-cyan-500/10 via-indigo-500/15 to-purple-600/10 blur-[130px] rounded-full pointer-events-none" />
      <div className="absolute top-20 right-10 w-72 h-72 bg-cyan-500/10 blur-[90px] rounded-full pointer-events-none" />
      <div className="absolute bottom-10 left-10 w-96 h-96 bg-purple-600/10 blur-[110px] rounded-full pointer-events-none" />

      {/* Grid Pattern overlay */}
      <div className="absolute inset-0 bg-grid-pattern opacity-40 pointer-events-none" />

      <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-4xl mx-auto">
          {/* Release Version Banner */}
          <motion.div
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5 }}
            className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-slate-900/90 border border-cyan-500/30 text-xs sm:text-sm font-medium text-slate-300 shadow-lg shadow-cyan-500/10 mb-8 backdrop-blur-md"
          >
            <span className="flex h-2 w-2 relative">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-cyan-400 opacity-75"></span>
              <span className="relative inline-flex rounded-full h-2 w-2 bg-cyan-500"></span>
            </span>
            <span className="font-semibold text-cyan-400">{CURRENT_RELEASE_VERSION} Available</span>
            <span className="text-slate-500">|</span>
            <span className="text-slate-400">Virtual KVM & Universal Smart Clipboard</span>
            <ChevronRight className="w-4 h-4 text-slate-500" />
          </motion.div>

          {/* Main Headline */}
          <motion.h1
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.1 }}
            className="text-4xl sm:text-6xl lg:text-7xl font-extrabold tracking-tight text-white leading-[1.12]"
          >
            Glide Between Desktops.{' '}
            <span className="bg-clip-text text-transparent bg-gradient-to-r from-cyan-400 via-indigo-300 to-fuchsia-400">
              Sync Clipboards Instantly.
            </span>{' '}
            Zero Hardware.
          </motion.h1>

          {/* Subtitle */}
          <motion.p
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="mt-6 text-lg sm:text-xl text-slate-300 max-w-2xl mx-auto font-normal leading-relaxed"
          >
            Seamless mouse and keyboard roaming across <strong className="text-white">macOS, Windows, and Linux</strong> with <span className="text-cyan-400 font-medium">&lt;3ms latency</span>.
            Plus end-to-end encrypted clipboard sync and a 3D Gyro Android companion air-mouse.
          </motion.p>

          {/* Primary OS Download & Action Section */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.3 }}
            className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4"
          >
            {/* Auto-detected Primary Download Button */}
            <a
              href={primaryDownload.downloadUrl}
              className="w-full sm:w-auto relative group overflow-hidden rounded-xl p-[2px] shadow-2xl shadow-cyan-500/25 hover:shadow-cyan-500/40 transition-all transform hover:-translate-y-0.5 active:translate-y-0"
            >
              <div className="absolute inset-0 bg-gradient-to-r from-cyan-500 via-indigo-500 to-fuchsia-500 animate-gradient" />
              <div className="relative px-7 py-4 bg-slate-950 rounded-[10px] flex items-center justify-center gap-3 transition-colors group-hover:bg-slate-900/90">
                <Download className="w-5 h-5 text-cyan-400 group-hover:animate-bounce" />
                <div className="text-left">
                  <div className="text-xs text-slate-400 uppercase tracking-wider font-semibold">
                    Download for {currentPlatform.name}
                  </div>
                  <div className="text-base font-bold text-white flex items-center gap-2">
                    <span>{primaryDownload.archLabel}</span>
                    <span className="text-xs px-2 py-0.5 rounded bg-cyan-500/20 text-cyan-300 border border-cyan-500/30">
                      {primaryDownload.size}
                    </span>
                  </div>
                </div>
              </div>
            </a>

            {/* Jump to All Downloads */}
            <a
              href="#downloads"
              className="w-full sm:w-auto px-6 py-4 rounded-xl bg-slate-900/80 hover:bg-slate-800 border border-slate-700/80 hover:border-slate-600 text-slate-200 hover:text-white font-semibold flex items-center justify-center gap-2 shadow-lg transition-all"
            >
              <span>Explore All Platforms</span>
              <ArrowRight className="w-4 h-4 text-cyan-400" />
            </a>
          </motion.div>

          {/* Quick Package Manager Terminal Snippet */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.4 }}
            className="mt-8 max-w-xl mx-auto"
          >
            <div className="bg-slate-950/90 border border-slate-800/90 rounded-xl p-3 shadow-xl backdrop-blur-md">
              {/* Terminal Tabs */}
              <div className="flex items-center justify-between border-b border-slate-800/80 pb-2 mb-2 px-1">
                <div className="flex items-center gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full bg-red-500/80" />
                  <div className="w-2.5 h-2.5 rounded-full bg-yellow-500/80" />
                  <div className="w-2.5 h-2.5 rounded-full bg-green-500/80" />
                  <span className="ml-2 text-xs text-slate-500 font-mono flex items-center gap-1">
                    <Terminal className="w-3.5 h-3.5 text-cyan-500" />
                    Quick Terminal Install
                  </span>
                </div>
                <div className="flex items-center gap-1">
                  {(['brew', 'winget', 'flatpak'] as const).map((tab) => (
                    <button
                      key={tab}
                      onClick={() => setSelectedCliTab(tab)}
                      className={`text-xs px-2 py-0.5 rounded transition-colors ${
                        selectedCliTab === tab
                          ? 'bg-slate-800 text-cyan-400 font-semibold border border-slate-700'
                          : 'text-slate-400 hover:text-slate-200'
                      }`}
                    >
                      {tab}
                    </button>
                  ))}
                </div>
              </div>

              {/* Code command */}
              <div className="flex items-center justify-between px-2 py-1 font-mono text-xs sm:text-sm">
                <div className="flex items-center gap-2 overflow-x-auto text-slate-300">
                  <span className="text-cyan-500 select-none">$</span>
                  <code>{cliCommands[selectedCliTab].cmd}</code>
                </div>
                <button
                  onClick={() =>
                    copyToClipboard(cliCommands[selectedCliTab].cmd, cliCommands[selectedCliTab].id)
                  }
                  className="ml-3 p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800/80 transition-colors flex-shrink-0"
                  title="Copy command"
                  aria-label="Copy package manager command"
                >
                  {copiedCmd === cliCommands[selectedCliTab].id ? (
                    <Check className="w-4 h-4 text-emerald-400" />
                  ) : (
                    <Copy className="w-4 h-4" />
                  )}
                </button>
              </div>
            </div>
          </motion.div>

          {/* Performance & Spec Metrics */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.5 }}
            className="mt-14 grid grid-cols-2 sm:grid-cols-4 gap-4 max-w-4xl mx-auto pt-8 border-t border-slate-800/60"
          >
            <div className="flex flex-col items-center p-3 rounded-xl bg-slate-900/40 border border-slate-800/50">
              <span className="text-2xl sm:text-3xl font-extrabold text-cyan-400 font-mono tracking-tight">
                &lt; 2.8ms
              </span>
              <span className="text-xs text-slate-400 mt-1 flex items-center gap-1 font-medium">
                <Zap className="w-3.5 h-3.5 text-cyan-400" />
                LAN Input Lag
              </span>
            </div>

            <div className="flex flex-col items-center p-3 rounded-xl bg-slate-900/40 border border-slate-800/50">
              <span className="text-2xl sm:text-3xl font-extrabold text-indigo-400 font-mono tracking-tight">
                1,000 Hz
              </span>
              <span className="text-xs text-slate-400 mt-1 flex items-center gap-1 font-medium">
                <Sparkles className="w-3.5 h-3.5 text-indigo-400" />
                Mouse Smoothing
              </span>
            </div>

            <div className="flex flex-col items-center p-3 rounded-xl bg-slate-900/40 border border-slate-800/50">
              <span className="text-2xl sm:text-3xl font-extrabold text-fuchsia-400 font-mono tracking-tight">
                AES-256
              </span>
              <span className="text-xs text-slate-400 mt-1 flex items-center gap-1 font-medium">
                <ShieldCheck className="w-3.5 h-3.5 text-fuchsia-400" />
                Zero-Knowledge E2EE
              </span>
            </div>

            <div className="flex flex-col items-center p-3 rounded-xl bg-slate-900/40 border border-slate-800/50">
              <span className="text-2xl sm:text-3xl font-extrabold text-emerald-400 font-mono tracking-tight">
                0 Bytes
              </span>
              <span className="text-xs text-slate-400 mt-1 flex items-center gap-1 font-medium">
                <Layers className="w-3.5 h-3.5 text-emerald-400" />
                GC Allocation
              </span>
            </div>
          </motion.div>
        </div>
      </div>
    </section>
  );
};
