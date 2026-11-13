'use client';

import React, { useState } from 'react';
import { motion } from 'framer-motion';
import { Terminal, ShieldCheck, Wifi, Copy, Check, ArrowRight, GitBranch, ClipboardList, Smartphone } from 'lucide-react';
import { REPO_URL } from '@/data/releases';

const CLONE_COMMAND = `git clone ${REPO_URL}.git && cd mouser && bash scripts/package-windows.sh`;

const facts = [
  { icon: Smartphone, value: 'Trackpad + air mouse', label: 'Android companion', color: 'text-cyan-600 dark:text-cyan-400' },
  { icon: ShieldCheck, value: 'PIN pairing', label: 'Only your phones', color: 'text-indigo-600 dark:text-indigo-400' },
  { icon: ClipboardList, value: 'Searchable', label: 'Clipboard history', color: 'text-fuchsia-600 dark:text-fuchsia-400' },
  { icon: Wifi, value: 'Local network', label: 'No account, no cloud', color: 'text-emerald-600 dark:text-emerald-400' },
];

export const Hero = () => {
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(CLONE_COMMAND);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard permission denied; the command stays selectable.
    }
  };

  return (
    <section className="relative pt-28 pb-20 md:pt-36 md:pb-28 overflow-hidden bg-radial-gradient">
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[700px] h-[400px] bg-gradient-to-tr from-cyan-500/10 via-indigo-500/15 to-purple-600/10 blur-[130px] rounded-full pointer-events-none" />
      <div className="absolute inset-0 bg-grid-pattern opacity-40 pointer-events-none" />

      <div className="relative max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-4xl mx-auto">
          <motion.div
            initial={{ opacity: 0, y: -20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.5 }}
            className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-white/90 dark:bg-slate-900/90 border border-slate-300/80 dark:border-cyan-500/30 text-xs sm:text-sm font-medium text-slate-700 dark:text-slate-300 mb-8"
          >
            <span className="relative inline-flex rounded-full h-2 w-2 bg-amber-500" />
            Early preview · open source
          </motion.div>

          <motion.h1
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.1 }}
            className="text-4xl sm:text-6xl lg:text-7xl font-extrabold tracking-tight text-slate-900 dark:text-white leading-[1.12]"
          >
            Your phone is the trackpad.{' '}
            <span className="bg-clip-text text-transparent bg-gradient-to-r from-cyan-600 via-indigo-600 to-fuchsia-600 dark:from-cyan-400 dark:via-indigo-300 dark:to-fuchsia-400">
              Your clipboard remembers.
            </span>
          </motion.h1>

          <motion.p
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.2 }}
            className="mt-6 text-lg sm:text-xl text-slate-600 dark:text-slate-300 max-w-2xl mx-auto leading-relaxed"
          >
            Mouser moves one mouse and keyboard across your{' '}
            <strong className="text-slate-900 dark:text-white font-semibold">computers</strong>, turns an Android
            phone into a trackpad and remote, and keeps a searchable, synced history of everything you copy. It all
            stays on your local network.
          </motion.p>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.3 }}
            className="mt-10 flex flex-col sm:flex-row items-center justify-center gap-4"
          >
            <a
              href="#downloads"
              className="w-full sm:w-auto px-7 py-4 rounded-2xl bg-gradient-to-r from-cyan-600 via-indigo-600 to-fuchsia-600 text-white font-bold flex items-center justify-center gap-2 shadow-xl shadow-cyan-500/20 hover:-translate-y-0.5 transition-transform"
            >
              Get Mouser
              <ArrowRight className="w-4 h-4" />
            </a>
            <a
              href={REPO_URL}
              className="w-full sm:w-auto px-6 py-4 rounded-xl bg-white dark:bg-slate-900/80 border border-slate-300 dark:border-slate-700/80 text-slate-700 dark:text-slate-200 font-semibold flex items-center justify-center gap-2 hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors"
            >
              <GitBranch className="w-4 h-4" />
              Source on GitHub
            </a>
          </motion.div>

          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.4 }}
            className="mt-8 max-w-2xl mx-auto"
          >
            <div className="bg-white/95 dark:bg-slate-950/90 border border-slate-200/90 dark:border-slate-800/90 rounded-xl p-3 text-left">
              <div className="flex items-center gap-2 border-b border-slate-200 dark:border-slate-800/80 pb-2 mb-2 px-1 text-xs text-slate-500 font-mono">
                <Terminal className="w-3.5 h-3.5 text-cyan-600 dark:text-cyan-500" />
                Build for Windows (needs the .NET 8 SDK)
              </div>
              <div className="flex items-center justify-between px-2 py-1 font-mono text-xs sm:text-sm">
                <div className="flex items-center gap-2 overflow-x-auto text-slate-800 dark:text-slate-300">
                  <span className="text-cyan-600 dark:text-cyan-500 select-none">$</span>
                  <code className="whitespace-nowrap select-all">{CLONE_COMMAND}</code>
                </div>
                <button
                  onClick={copy}
                  className="ml-3 p-1.5 rounded-lg text-slate-500 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white hover:bg-slate-100 dark:hover:bg-slate-800/80 flex-shrink-0"
                  aria-label="Copy build command"
                >
                  {copied ? <Check className="w-4 h-4 text-emerald-500" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>
            </div>
          </motion.div>

          <motion.ul
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.6, delay: 0.5 }}
            className="mt-14 grid grid-cols-2 sm:grid-cols-4 gap-4 max-w-4xl mx-auto pt-8 border-t border-slate-200 dark:border-slate-800/60"
          >
            {facts.map(({ icon: Icon, value, label, color }) => (
              <li
                key={label}
                className="flex flex-col items-center p-3 rounded-xl bg-white/70 dark:bg-slate-900/40 border border-slate-200/80 dark:border-slate-800/50"
              >
                <Icon className={`w-5 h-5 ${color}`} aria-hidden />
                <span className={`mt-2 text-base font-bold ${color}`}>{value}</span>
                <span className="text-xs text-slate-600 dark:text-slate-400 mt-1">{label}</span>
              </li>
            ))}
          </motion.ul>
        </div>
      </div>
    </section>
  );
};
