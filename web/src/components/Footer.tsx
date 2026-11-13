import React from 'react';
import { MousePointer, Shield, ArrowUpRight } from 'lucide-react';
import { REPO_URL } from '@/data/releases';

export const Footer = () => {
  return (
    <footer className="bg-slate-50 dark:bg-slate-950 border-t border-slate-200 dark:border-slate-800/80 pt-16 pb-12 text-slate-600 dark:text-slate-400 text-sm transition-colors duration-200">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-10 pb-12 border-b border-slate-200 dark:border-slate-800/80">
          {/* Brand info */}
          <div className="md:col-span-1 space-y-4">
            <div className="flex items-center gap-2.5">
              <div className="w-8 h-8 rounded-lg bg-gradient-to-tr from-cyan-500 to-indigo-600 flex items-center justify-center p-[1px]">
                <div className="w-full h-full bg-white dark:bg-slate-950 rounded-[7px] flex items-center justify-center transition-colors">
                  <MousePointer className="w-4 h-4 text-cyan-500 dark:text-cyan-400" />
                </div>
              </div>
              <span className="font-extrabold text-lg text-slate-900 dark:text-white">Mouser</span>
            </div>
            <p className="text-xs text-slate-600 dark:text-slate-400 leading-relaxed">
              Use your Android phone as a trackpad and remote for your computer, with a local clipboard history.
            </p>
          </div>

          {/* Quick links: Downloads */}
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white mb-3">Downloads</h4>
            <ul className="space-y-2 text-xs">
              {['Windows', 'macOS', 'Linux (clipboard only)', 'Android companion'].map((label) => (
                <li key={label}>
                  <a href="#downloads" className="hover:text-cyan-600 dark:hover:text-cyan-400 transition-colors">
                    {label}
                  </a>
                </li>
              ))}
            </ul>
          </div>

          {/* Project & Community */}
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white mb-3">Resources</h4>
            <ul className="space-y-2 text-xs">
              <li>
                <a
                  href={REPO_URL}
                  target="_blank"
                  rel="noreferrer"
                  className="hover:text-cyan-600 dark:hover:text-cyan-400 transition-colors flex items-center gap-1"
                >
                  <span>GitHub Repository</span>
                  <ArrowUpRight className="w-3 h-3 text-slate-400" />
                </a>
              </li>
              <li>
                <a href="#architecture" className="hover:text-cyan-600 dark:hover:text-cyan-400 transition-colors">
                  How it works
                </a>
              </li>
            </ul>
          </div>

          {/* Security & Open Source */}
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-900 dark:text-white mb-3">Integrity</h4>
            <div className="p-3.5 rounded-xl bg-white dark:bg-slate-900/60 border border-slate-200 dark:border-slate-800 space-y-2 text-xs shadow-sm dark:shadow-none">
              <div className="flex items-center gap-2 text-slate-900 dark:text-white font-semibold">
                <Shield className="w-4 h-4 text-emerald-500 dark:text-emerald-400" />
                <span>Stays on your network</span>
              </div>
              <p className="text-[11px] text-slate-600 dark:text-slate-400 leading-normal">
                No accounts and no cloud. Input travels directly between your phone and computer, and clipboard history never leaves the computer. Phone traffic is not encrypted yet, so use it on networks you trust.
              </p>
            </div>
          </div>
        </div>

        {/* Bottom copyright */}
        <div className="mt-8 flex flex-col sm:flex-row items-center justify-between gap-4 text-xs text-slate-500">
          <p>© 2026 Mouser contributors.</p>
          <div className="flex items-center gap-1">
            <span>Crafted with Next.js, Tailwind CSS & Framer Motion</span>
          </div>
        </div>
      </div>
    </footer>
  );
};
