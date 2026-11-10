import React from 'react';
import { MousePointer, Shield, ArrowUpRight } from 'lucide-react';
import { CURRENT_RELEASE_VERSION } from '@/data/releases';

export const Footer = () => {
  return (
    <footer className="bg-slate-950 border-t border-slate-800/80 pt-16 pb-12 text-slate-400 text-sm">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10 pb-12 border-b border-slate-800/80">
          {/* Brand info */}
          <div className="md:col-span-1 space-y-4">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-gradient-to-tr from-cyan-500 to-indigo-600 flex items-center justify-center p-[1px]">
                <div className="w-full h-full bg-slate-950 rounded-[7px] flex items-center justify-center">
                  <MousePointer className="w-4 h-4 text-cyan-400" />
                </div>
              </div>
              <span className="font-extrabold text-lg text-white">Mouser</span>
            </div>
            <p className="text-xs text-slate-400 leading-relaxed">
              Unified cross-platform Virtual KVM switch and universal smart clipboard engine built for speed, privacy, and zero lag.
            </p>
            <div className="text-xs font-mono text-cyan-400">
              Current Release: {CURRENT_RELEASE_VERSION}
            </div>
          </div>

          {/* Quick links: Downloads */}
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-white mb-3">Downloads</h4>
            <ul className="space-y-2 text-xs">
              <li>
                <a href="#downloads" className="hover:text-cyan-400 transition-colors">
                  macOS (Apple Silicon & Intel DMG)
                </a>
              </li>
              <li>
                <a href="#downloads" className="hover:text-cyan-400 transition-colors">
                  Windows (x64 & ARM64 MSI)
                </a>
              </li>
              <li>
                <a href="#downloads" className="hover:text-cyan-400 transition-colors">
                  Linux (AppImage, Flatpak, DEB)
                </a>
              </li>
              <li>
                <a href="#downloads" className="hover:text-cyan-400 transition-colors">
                  Android Companion APK
                </a>
              </li>
              <li>
                <a href="#downloads" className="hover:text-cyan-400 transition-colors">
                  iOS TestFlight Beta
                </a>
              </li>
            </ul>
          </div>

          {/* Project & Community */}
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-white mb-3">Resources</h4>
            <ul className="space-y-2 text-xs">
              <li>
                <a
                  href="https://github.com/mouser-org/mouser"
                  target="_blank"
                  rel="noreferrer"
                  className="hover:text-cyan-400 transition-colors flex items-center gap-1"
                >
                  <span>GitHub Repository</span>
                  <ArrowUpRight className="w-3 h-3 text-slate-500" />
                </a>
              </li>
              <li>
                <a href="#architecture" className="hover:text-cyan-400 transition-colors">
                  Architecture & Specifications
                </a>
              </li>
              <li>
                <a href="#demo" className="hover:text-cyan-400 transition-colors">
                  Interactive Simulator
                </a>
              </li>
              <li>
                <a href="#comparison" className="hover:text-cyan-400 transition-colors">
                  Feature Comparison
                </a>
              </li>
            </ul>
          </div>

          {/* Security & Open Source */}
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-white mb-3">Integrity</h4>
            <div className="p-3.5 rounded-xl bg-slate-900/60 border border-slate-800 space-y-2 text-xs">
              <div className="flex items-center gap-2 text-white font-semibold">
                <Shield className="w-4 h-4 text-emerald-400" />
                <span>100% Zero-Knowledge</span>
              </div>
              <p className="text-[11px] text-slate-400 leading-normal">
                All mouse packets and clipboard payloads stay on your local network. Protected by AES-256-GCM authenticated encryption.
              </p>
            </div>
          </div>
        </div>

        {/* Bottom copyright */}
        <div className="mt-8 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-500">
          <p>© 2026 Mouser Open Source Project. Released under MIT / Apache 2.0.</p>
          <div className="flex items-center gap-1">
            <span>Crafted with Next.js, Tailwind CSS & Framer Motion</span>
          </div>
        </div>
      </div>
    </footer>
  );
};
