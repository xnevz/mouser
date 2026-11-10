'use client';

import React from 'react';
import { Check, X, Sparkles } from 'lucide-react';

export const ComparisonTable = () => {
  const comparisons = [
    {
      feature: 'Supported Platforms',
      mouser: 'macOS, Windows, Linux, Android, iOS',
      synergy: 'macOS, Windows, Linux',
      barrier: 'macOS, Windows, Linux (unmaintained)',
      shareMouse: 'macOS, Windows only',
      universalControl: 'Apple hardware only',
    },
    {
      feature: '1,000 Hz Mouse Polling Smoothing',
      mouser: true,
      synergy: false,
      barrier: false,
      shareMouse: false,
      universalControl: true,
    },
    {
      feature: 'Zero-Knowledge E2EE Clipboard Sync',
      mouser: true,
      synergy: 'Partial (SSL)',
      barrier: 'Partial (OpenSSL)',
      shareMouse: false,
      universalControl: true,
    },
    {
      feature: 'Android 3D Gyro Air Mouse & Trackpad',
      mouser: true,
      synergy: false,
      barrier: false,
      shareMouse: false,
      universalControl: false,
    },
    {
      feature: 'Wayland Native Input Injection',
      mouser: true,
      synergy: 'Experimental',
      barrier: false,
      shareMouse: false,
      universalControl: false,
    },
    {
      feature: 'Cross-Screen Drag & Drop Streaming',
      mouser: true,
      synergy: 'Paid Pro version only',
      barrier: false,
      shareMouse: true,
      universalControl: true,
    },
    {
      feature: 'Pricing & Licensing',
      mouser: '100% Free & Open Source',
      synergy: '$29 - $39 / device',
      barrier: 'Free (abandoned fork)',
      shareMouse: '$49 - $149 / license',
      universalControl: 'Requires all-Apple gear',
    },
  ];

  return (
    <section id="comparison" className="py-24 relative bg-slate-100/60 dark:bg-slate-950 border-t border-slate-200 dark:border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="text-center max-w-3xl mx-auto mb-16">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-cyan-500/10 border border-cyan-500/20 text-cyan-600 dark:text-cyan-400 text-xs font-semibold uppercase tracking-wider mb-3">
            <Sparkles className="w-3.5 h-3.5" />
            Clear Advantage
          </div>
          <h2 className="text-3xl sm:text-5xl font-extrabold text-slate-900 dark:text-white tracking-tight">
            How Mouser Compares
          </h2>
          <p className="mt-4 text-base sm:text-lg text-slate-600 dark:text-slate-300">
            Why engineers, streamers, and multi-computer setups are switching to Mouser.
          </p>
        </div>

        {/* Comparison Table */}
        <div className="rounded-2xl border border-slate-200 dark:border-slate-800 bg-white/95 dark:bg-slate-900/60 overflow-hidden shadow-xl dark:shadow-2xl backdrop-blur-md">
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm text-slate-700 dark:text-slate-300">
              <thead className="bg-slate-50 dark:bg-slate-950/80 text-xs uppercase tracking-wider text-slate-500 dark:text-slate-400 border-b border-slate-200 dark:border-slate-800">
                <tr>
                  <th className="py-4 px-6 font-semibold">Capability</th>
                  <th className="py-4 px-6 font-bold text-cyan-700 dark:text-cyan-400 bg-cyan-50 dark:bg-cyan-500/10 border-x border-cyan-200 dark:border-cyan-500/20">
                    Mouser (Ours)
                  </th>
                  <th className="py-4 px-6 font-semibold">Synergy 3</th>
                  <th className="py-4 px-6 font-semibold">Barrier</th>
                  <th className="py-4 px-6 font-semibold">ShareMouse</th>
                  <th className="py-4 px-6 font-semibold">Universal Control</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-200 dark:divide-slate-800/60">
                {comparisons.map((row, idx) => (
                  <tr key={idx} className="hover:bg-slate-50/70 dark:hover:bg-slate-800/30 transition-colors">
                    <td className="py-4 px-6 font-medium text-slate-900 dark:text-white">{row.feature}</td>
                    
                    {/* Mouser Column */}
                    <td className="py-4 px-6 bg-cyan-50/50 dark:bg-cyan-500/5 font-semibold text-cyan-800 dark:text-cyan-300 border-x border-cyan-200 dark:border-cyan-500/20">
                      {typeof row.mouser === 'boolean' ? (
                        <span className="flex items-center gap-1.5 text-cyan-600 dark:text-cyan-400">
                          <Check className="w-4 h-4 stroke-[3]" /> Yes
                        </span>
                      ) : (
                        row.mouser
                      )}
                    </td>

                    {/* Synergy Column */}
                    <td className="py-4 px-6 text-slate-500 dark:text-slate-400">
                      {typeof row.synergy === 'boolean' ? (
                        row.synergy ? (
                          <Check className="w-4 h-4 text-emerald-500 dark:text-emerald-400" />
                        ) : (
                          <X className="w-4 h-4 text-slate-400 dark:text-slate-600" />
                        )
                      ) : (
                        row.synergy
                      )}
                    </td>

                    {/* Barrier Column */}
                    <td className="py-4 px-6 text-slate-500 dark:text-slate-400">
                      {typeof row.barrier === 'boolean' ? (
                        row.barrier ? (
                          <Check className="w-4 h-4 text-emerald-500 dark:text-emerald-400" />
                        ) : (
                          <X className="w-4 h-4 text-slate-400 dark:text-slate-600" />
                        )
                      ) : (
                        row.barrier
                      )}
                    </td>

                    {/* ShareMouse Column */}
                    <td className="py-4 px-6 text-slate-500 dark:text-slate-400">
                      {typeof row.shareMouse === 'boolean' ? (
                        row.shareMouse ? (
                          <Check className="w-4 h-4 text-emerald-500 dark:text-emerald-400" />
                        ) : (
                          <X className="w-4 h-4 text-slate-400 dark:text-slate-600" />
                        )
                      ) : (
                        row.shareMouse
                      )}
                    </td>

                    {/* Universal Control Column */}
                    <td className="py-4 px-6 text-slate-500 dark:text-slate-400">
                      {typeof row.universalControl === 'boolean' ? (
                        row.universalControl ? (
                          <Check className="w-4 h-4 text-emerald-500 dark:text-emerald-400" />
                        ) : (
                          <X className="w-4 h-4 text-slate-400 dark:text-slate-600" />
                        )
                      ) : (
                        row.universalControl
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </section>
  );
};
