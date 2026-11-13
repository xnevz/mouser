'use client';

import React from 'react';
import { motion } from 'framer-motion';
import { MousePointer2, ClipboardList, Navigation, Radar, ShieldCheck, Music, Monitor } from 'lucide-react';

const features = [
  {
    icon: Monitor,
    title: 'One mouse and keyboard, several computers',
    body: 'Pair two computers, say which side the other one is on, and push the cursor past that edge to control it. Copied text follows you, and you can send files across. Ctrl+Alt+Esc always brings you back.',
    accent: 'text-sky-600 dark:text-sky-400 bg-sky-500/10 border-sky-500/25',
    wide: true,
  },
  {
    icon: MousePointer2,
    title: 'A real trackpad on your phone',
    body: 'Move with one finger, scroll with two, tap to click and two-finger tap to right-click. Hold the Left button to drag windows and select text.',
    accent: 'text-cyan-600 dark:text-cyan-400 bg-cyan-500/10 border-cyan-500/25',
  },
  {
    icon: Navigation,
    title: 'Air mouse',
    body: 'Hold the trigger and point the phone at the screen. The gyroscope only runs while you hold it, so it costs no battery otherwise.',
    accent: 'text-emerald-600 dark:text-emerald-400 bg-emerald-500/10 border-emerald-500/25',
  },
  {
    icon: Music,
    title: 'Media and number keys',
    body: 'Play/pause, track skip, volume, arrow keys and a full number pad, sent as real keyboard keys.',
    accent: 'text-amber-600 dark:text-amber-400 bg-amber-500/10 border-amber-500/25',
  },
  {
    icon: ClipboardList,
    title: 'Clipboard history',
    body: 'Everything you copy is saved locally and searchable. Press Win+Alt+V in any app to pick a clip and paste it. Links and code are detected automatically, and things that look like passwords or API keys are skipped.',
    accent: 'text-fuchsia-600 dark:text-fuchsia-400 bg-fuchsia-500/10 border-fuchsia-500/25',
  },
  {
    icon: Radar,
    title: 'Finds your computer',
    body: 'Computers running Mouser announce themselves on the local network, so the phone lists them without typing an address.',
    accent: 'text-indigo-600 dark:text-indigo-400 bg-indigo-500/10 border-indigo-500/25',
  },
  {
    icon: ShieldCheck,
    title: 'PIN pairing',
    body: 'A phone must enter the 6-digit PIN shown on the computer before it can control it. Repeated wrong guesses are locked out, and a new PIN unpairs everyone.',
    accent: 'text-rose-600 dark:text-rose-400 bg-rose-500/10 border-rose-500/25',
    wide: true,
  },
];

export const FeatureGrid = () => (
  <section id="features" className="py-24 relative">
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
      <div className="text-center max-w-3xl mx-auto mb-16">
        <h2 className="text-3xl sm:text-5xl font-extrabold text-slate-900 dark:text-white tracking-tight">What Mouser does today</h2>
        <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
          Everything below works today on Windows. macOS and Linux support varies, see Get Mouser.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {features.map(({ icon: Icon, title, body, accent, wide }, i) => (
          <motion.div
            key={title}
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            transition={{ duration: 0.4, delay: i * 0.05 }}
            className={`${wide ? 'md:col-span-2' : ''} rounded-3xl bg-white/95 dark:bg-slate-900/85 border border-slate-200 dark:border-slate-800/90 p-7 shadow-lg`}
          >
            <div className={`inline-flex p-3 rounded-2xl border ${accent}`}>
              <Icon className="w-6 h-6" aria-hidden />
            </div>
            <h3 className="mt-5 text-xl font-bold text-slate-900 dark:text-white">{title}</h3>
            <p className="mt-2 text-sm sm:text-base text-slate-600 dark:text-slate-300 leading-relaxed">{body}</p>
          </motion.div>
        ))}
      </div>
    </div>
  </section>
);
