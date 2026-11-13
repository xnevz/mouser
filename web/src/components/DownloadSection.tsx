'use client';

import React, { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Download, Check, Copy, Monitor, Laptop, Terminal, Smartphone, CircleCheck, CircleDashed, GitBranch } from 'lucide-react';
import { PLATFORMS, RELEASES_URL } from '@/data/releases';
import { PlatformId } from '@/types/download';
import { StretchyPillTabs, TabOption } from '@/components/StretchyPillTabs';

const platformTabs: TabOption<PlatformId>[] = [
  { id: 'windows', label: 'Windows', icon: Monitor, badge: PLATFORMS.windows.badge },
  { id: 'macos', label: 'macOS', icon: Laptop, badge: PLATFORMS.macos.badge },
  { id: 'linux', label: 'Linux', icon: Terminal, badge: PLATFORMS.linux.badge },
  { id: 'android', label: 'Android', icon: Smartphone, badge: PLATFORMS.android.badge },
];

export const DownloadSection = () => {
  const [selected, setSelected] = useState<PlatformId>('windows');
  const [copied, setCopied] = useState(false);
  const platform = PLATFORMS[selected];

  const copyCommand = async () => {
    try {
      await navigator.clipboard.writeText(platform.buildCommand);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch {
      // Clipboard permission denied: the command is still selectable on screen.
    }
  };

  return (
    <section id="downloads" className="py-24 relative">
      <div className="max-w-6xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto mb-12">
          <h2 className="text-3xl sm:text-5xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            Get Mouser
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
            Mouser is early, open-source software. There are no prebuilt downloads yet. Build it from source with one
            command, or check the releases page for builds.
          </p>
        </div>

        <div className="flex justify-center mb-10">
          <StretchyPillTabs
            tabs={platformTabs}
            activeId={selected}
            onChange={setSelected}
            pillColor="from-cyan-500 via-indigo-500 to-blue-600"
            layoutId="platform-download-active-pill"
          />
        </div>

        <AnimatePresence mode="wait">
          <motion.div
            key={selected}
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, y: -12 }}
            transition={{ duration: 0.2 }}
            className="rounded-3xl bg-white/95 dark:bg-slate-900/90 border border-slate-200 dark:border-slate-800 p-6 sm:p-10 shadow-xl"
          >
            <div className="flex flex-col md:flex-row md:items-start justify-between gap-6 pb-8 border-b border-slate-200 dark:border-slate-800">
              <div>
                <div className="flex flex-wrap items-center gap-3">
                  <h3 className="text-2xl sm:text-3xl font-extrabold text-slate-900 dark:text-white">{platform.name}</h3>
                  <span className="px-3 py-1 rounded-full text-xs font-semibold bg-cyan-500/10 text-cyan-700 dark:text-cyan-300 border border-cyan-500/30">
                    {platform.badge}
                  </span>
                </div>
                <p className="mt-2 text-slate-600 dark:text-slate-300 max-w-2xl">{platform.summary}</p>
                <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">Requires: {platform.requirements}</p>
              </div>
              <a
                href={RELEASES_URL}
                className="inline-flex items-center justify-center gap-2 px-5 py-3 rounded-xl bg-slate-900 dark:bg-white text-white dark:text-slate-900 font-semibold text-sm whitespace-nowrap hover:opacity-90 transition-opacity"
              >
                <GitBranch className="w-4 h-4" />
                Releases on GitHub
              </a>
            </div>

            <div className="mt-8 grid grid-cols-1 md:grid-cols-2 gap-8">
              <div>
                <h4 className="text-sm font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-3">Works today</h4>
                <ul className="space-y-2">
                  {platform.works.map((item) => (
                    <li key={item} className="flex gap-2 text-sm text-slate-700 dark:text-slate-200">
                      <CircleCheck className="w-4 h-4 mt-0.5 flex-shrink-0 text-emerald-500" aria-hidden />
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
              <div>
                <h4 className="text-sm font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-3">Not yet</h4>
                <ul className="space-y-2">
                  {platform.notYet.map((item) => (
                    <li key={item} className="flex gap-2 text-sm text-slate-500 dark:text-slate-400">
                      <CircleDashed className="w-4 h-4 mt-0.5 flex-shrink-0" aria-hidden />
                      {item}
                    </li>
                  ))}
                </ul>
              </div>
            </div>

            <div className="mt-10 pt-8 border-t border-slate-200 dark:border-slate-800">
              <h4 className="text-sm font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-3 flex items-center gap-2">
                <Download className="w-4 h-4" aria-hidden />
                Build from source
              </h4>
              <div className="flex items-center justify-between gap-3 px-4 py-3 rounded-xl bg-slate-100 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 font-mono text-sm text-slate-800 dark:text-cyan-300">
                <code className="overflow-x-auto whitespace-nowrap select-all">{platform.buildCommand}</code>
                <button
                  onClick={copyCommand}
                  className="p-1.5 rounded hover:bg-slate-200 dark:hover:bg-slate-800 text-slate-500 dark:text-slate-400"
                  aria-label="Copy build command"
                >
                  {copied ? <Check className="w-4 h-4 text-emerald-500" /> : <Copy className="w-4 h-4" />}
                </button>
              </div>
              <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
                Run from the repository root. Output: <code className="font-mono">{platform.buildOutput}</code>
              </p>

              <ol className="mt-6 list-decimal list-inside space-y-1 text-sm text-slate-600 dark:text-slate-300">
                {platform.setup.map((step) => (
                  <li key={step}>{step}</li>
                ))}
              </ol>
            </div>
          </motion.div>
        </AnimatePresence>
      </div>
    </section>
  );
};
