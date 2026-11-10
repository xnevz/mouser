'use client';

import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Download,
  Terminal,
  ShieldCheck,
  Check,
  Copy,
  Laptop,
  Monitor,
  Terminal as TerminalIcon,
  Smartphone,
  Tablet,
  FileCheck,
  Info,
  Key
} from 'lucide-react';
import { PLATFORM_RELEASES, RELEASE_DATE } from '@/data/releases';
import { PlatformId, DownloadArtifact } from '@/types/download';
import { StretchyPillTabs, TabOption } from '@/components/StretchyPillTabs';

export const DownloadSection = () => {
  const [selectedPlatform, setSelectedPlatform] = useState<PlatformId>('macos');
  const [copiedSha, setCopiedSha] = useState<string | null>(null);
  const [copiedPkgCmd, setCopiedPkgCmd] = useState<string | null>(null);
  const [activeChecksumModal, setActiveChecksumModal] = useState<DownloadArtifact | null>(null);

  const currentData = PLATFORM_RELEASES[selectedPlatform];

  const handleCopySha = (sha: string) => {
    navigator.clipboard.writeText(sha);
    setCopiedSha(sha);
    setTimeout(() => setCopiedSha(null), 2500);
  };

  const handleCopyPkgCmd = (cmd: string, id: string) => {
    navigator.clipboard.writeText(cmd);
    setCopiedPkgCmd(id);
    setTimeout(() => setCopiedPkgCmd(null), 2500);
  };

  const platformTabs: TabOption<PlatformId>[] = [
    { id: 'macos', label: 'macOS', icon: Laptop, badge: 'Apple Silicon / Intel' },
    { id: 'windows', label: 'Windows', icon: Monitor, badge: 'MSI x64 / ARM64' },
    { id: 'linux', label: 'Linux', icon: TerminalIcon, badge: 'AppImage / deb' },
    { id: 'android', label: 'Android', icon: Smartphone, badge: 'Companion APK' },
    { id: 'ios', label: 'iOS / iPadOS', icon: Tablet, badge: 'TestFlight' },
  ];

  return (
    <section id="downloads" className="py-24 relative">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Section Heading */}
        <div className="text-center max-w-3xl mx-auto mb-16">
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-cyan-500/10 border border-cyan-500/30 text-cyan-400 text-xs font-semibold uppercase tracking-wider mb-4 shadow-[0_0_15px_rgba(6,182,212,0.15)]">
            <Download className="w-3.5 h-3.5" />
            Universal Multi-Platform Binaries
          </div>
          <h2 className="text-3xl sm:text-5xl font-extrabold text-white tracking-tight">
            Download Mouser for Every Screen
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-300">
            Native, cryptographically signed binaries compiled for macOS, Windows, Linux, Android, and iOS. Zero telemetry, zero bloat, completely free and open source.
          </p>
        </div>

        {/* Elastic Stretchy Tabs with Full Corner Radius */}
        <div className="flex justify-center mb-12">
          <StretchyPillTabs
            tabs={platformTabs}
            activeId={selectedPlatform}
            onChange={(id) => setSelectedPlatform(id)}
            pillColor="from-cyan-500 via-indigo-500 to-blue-600"
            layoutId="platform-download-active-pill"
          />
        </div>

        {/* Selected Platform Detailed Card with Bouncy Switch Transitions */}
        <AnimatePresence mode="wait">
          <motion.div
            key={selectedPlatform}
            initial={{ opacity: 0, y: 16, scale: 0.98 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -16, scale: 0.98 }}
            transition={{ type: 'spring', stiffness: 350, damping: 25 }}
            className="rounded-3xl bg-slate-900/90 border border-slate-800 p-6 sm:p-10 shadow-2xl backdrop-blur-2xl relative overflow-hidden"
          >
            {/* Subtle background glow */}
            <div className="absolute top-0 right-0 w-80 h-80 bg-cyan-500/10 blur-[100px] pointer-events-none rounded-full" />
            <div className="absolute bottom-0 left-0 w-80 h-80 bg-purple-500/10 blur-[100px] pointer-events-none rounded-full" />

          {/* Platform Header Info */}
          <div className="flex flex-col md:flex-row md:items-center justify-between gap-6 pb-8 border-b border-slate-800">
            <div>
              <div className="flex items-center gap-3">
                <h3 className="text-2xl sm:text-3xl font-extrabold text-white">{currentData.name}</h3>
                <span className="px-3 py-1 rounded-full text-xs font-semibold bg-cyan-500/10 text-cyan-400 border border-cyan-500/30">
                  {currentData.badge}
                </span>
                <span className="text-xs text-slate-400 font-mono">
                  {currentData.latestVersion} ({RELEASE_DATE})
                </span>
              </div>
              <p className="mt-2 text-slate-300 text-sm sm:text-base max-w-2xl">
                {currentData.subtitle}
              </p>
              <div className="mt-2 flex items-center gap-2 text-xs text-slate-400 font-mono">
                <Info className="w-3.5 h-3.5 text-cyan-400" />
                <span>Compatible with: {currentData.supportedVersions}</span>
              </div>
            </div>

            {/* Quick Primary Download CTA */}
            <div className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3">
              <a
                href={currentData.primaryArtifact.downloadUrl}
                className="inline-flex items-center justify-center gap-2 px-6 py-3.5 rounded-xl bg-gradient-to-r from-cyan-500 to-indigo-600 hover:from-cyan-400 hover:to-indigo-500 text-white font-bold text-sm shadow-xl shadow-cyan-500/25 transition-all transform hover:-translate-y-0.5"
              >
                <Download className="w-4 h-4" />
                <span>Download Primary Installer</span>
                <span className="text-xs bg-slate-950/40 px-2 py-0.5 rounded font-mono">
                  {currentData.primaryArtifact.size}
                </span>
              </a>
            </div>
          </div>

          {/* Artifacts Grid & Architectures */}
          <div className="mt-8">
            <h4 className="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-4 flex items-center gap-2">
              <FileCheck className="w-4 h-4 text-cyan-400" />
              Available Architectures & Packages
            </h4>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {currentData.artifacts.map((artifact, idx) => (
                <div
                  key={idx}
                  className="rounded-2xl bg-slate-950/80 border border-slate-800/80 hover:border-slate-700 p-5 flex flex-col justify-between transition-all group"
                >
                  <div>
                    <div className="flex items-start justify-between gap-2">
                      <span className="font-bold text-white text-base group-hover:text-cyan-300 transition-colors">
                        {artifact.title}
                      </span>
                      {artifact.recommended && (
                        <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/30 whitespace-nowrap">
                          RECOMMENDED
                        </span>
                      )}
                    </div>
                    <p className="mt-1 font-mono text-xs text-slate-400 truncate">
                      {artifact.filename}
                    </p>
                    <div className="mt-3 flex items-center gap-2 text-xs text-slate-400 font-mono">
                      <span className="px-2 py-0.5 rounded bg-slate-900 border border-slate-800 text-slate-300">
                        {artifact.archLabel}
                      </span>
                      <span>•</span>
                      <span>{artifact.size}</span>
                    </div>
                  </div>

                  <div className="mt-6 pt-4 border-t border-slate-800/80 flex items-center justify-between gap-2">
                    <button
                      onClick={() => setActiveChecksumModal(artifact)}
                      className="text-xs text-slate-400 hover:text-cyan-400 flex items-center gap-1 transition-colors font-mono"
                    >
                      <Key className="w-3.5 h-3.5" />
                      <span>SHA256</span>
                    </button>

                    <a
                      href={artifact.downloadUrl}
                      className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-white text-xs font-semibold transition-colors"
                    >
                      <Download className="w-3.5 h-3.5 text-cyan-400" />
                      <span>Get File</span>
                    </a>
                  </div>
                </div>
              ))}
            </div>
          </div>

          {/* Package Managers Section */}
          {currentData.packageManagers.length > 0 && (
            <div className="mt-10 pt-8 border-t border-slate-800">
              <h4 className="text-sm font-semibold uppercase tracking-wider text-slate-400 mb-4 flex items-center gap-2">
                <Terminal className="w-4 h-4 text-cyan-400" />
                Package Manager & CLI Installation
              </h4>

              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {currentData.packageManagers.map((pkg, idx) => (
                  <div
                    key={idx}
                    className="rounded-xl bg-slate-950/90 border border-slate-800 p-4 flex flex-col justify-between"
                  >
                    <div>
                      <div className="flex items-center justify-between">
                        <span className="font-bold text-white text-sm">{pkg.name}</span>
                        <span className="text-[11px] text-slate-400">{pkg.description}</span>
                      </div>
                      <div className="mt-2 flex items-center justify-between px-3 py-2 rounded-lg bg-slate-900 border border-slate-800/90 font-mono text-xs text-cyan-300">
                        <span className="truncate">{pkg.command}</span>
                        <button
                          onClick={() => handleCopyPkgCmd(pkg.command, `${selectedPlatform}-${idx}`)}
                          className="ml-2 p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white transition-colors"
                          title="Copy command"
                        >
                          {copiedPkgCmd === `${selectedPlatform}-${idx}` ? (
                            <Check className="w-4 h-4 text-emerald-400" />
                          ) : (
                            <Copy className="w-4 h-4" />
                          )}
                        </button>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Quick Setup Instructions */}
          <div className="mt-8 pt-6 border-t border-slate-800 text-xs text-slate-400">
            <span className="font-semibold text-slate-300 uppercase tracking-wider block mb-2">
              Setup Instructions:
            </span>
            <ul className="list-disc list-inside space-y-1">
              {currentData.quickInstructions.map((step, idx) => (
                <li key={idx} className="leading-relaxed">
                  {step}
                </li>
              ))}
            </ul>
          </div>
        </motion.div>
        </AnimatePresence>

        {/* SHA-256 Checksum Modal */}
        {activeChecksumModal && (
          <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-sm">
            <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 shadow-2xl relative">
              <div className="flex items-center justify-between pb-3 border-b border-slate-800">
                <div className="flex items-center gap-2 text-white font-bold text-base">
                  <ShieldCheck className="w-5 h-5 text-cyan-400" />
                  <span>Verify SHA-256 Checksum</span>
                </div>
                <button
                  onClick={() => setActiveChecksumModal(null)}
                  className="text-slate-400 hover:text-white text-lg font-bold"
                >
                  ✕
                </button>
              </div>

              <div className="mt-4 space-y-3">
                <div>
                  <span className="text-xs text-slate-400 block">Filename:</span>
                  <span className="font-mono text-xs text-white">{activeChecksumModal.filename}</span>
                </div>
                <div>
                  <span className="text-xs text-slate-400 block">SHA-256 Checksum:</span>
                  <div className="mt-1 p-2.5 rounded-lg bg-slate-950 border border-slate-800 font-mono text-[11px] text-cyan-300 break-all select-all flex items-center justify-between gap-2">
                    <span>{activeChecksumModal.sha256}</span>
                    <button
                      onClick={() => handleCopySha(activeChecksumModal.sha256)}
                      className="p-1 rounded bg-slate-900 hover:bg-slate-800 text-slate-300 hover:text-white flex-shrink-0"
                    >
                      {copiedSha === activeChecksumModal.sha256 ? (
                        <Check className="w-3.5 h-3.5 text-emerald-400" />
                      ) : (
                        <Copy className="w-3.5 h-3.5" />
                      )}
                    </button>
                  </div>
                </div>

                <div className="p-3 rounded-lg bg-slate-950/60 border border-slate-800 text-[11px] text-slate-400 space-y-1">
                  <span className="font-semibold text-slate-300 block">How to verify on your terminal:</span>
                  <p className="font-mono text-cyan-400">
                    shasum -a 256 {activeChecksumModal.filename}
                  </p>
                </div>
              </div>

              <div className="mt-6 flex justify-end">
                <button
                  onClick={() => setActiveChecksumModal(null)}
                  className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-white text-xs font-semibold transition-colors"
                >
                  Close
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </section>
  );
};
