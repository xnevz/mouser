'use client';

import React, { useState } from 'react';
import { motion } from 'framer-motion';

export interface TabOption<T extends string = string> {
  id: T;
  label: string;
  icon?: React.ComponentType<{ className?: string }>;
  badge?: string;
}

interface StretchyPillTabsProps<T extends string = string> {
  tabs: TabOption<T>[];
  activeId: T;
  onChange: (id: T) => void;
  className?: string;
  pillColor?: string;
  layoutId?: string;
}

export function StretchyPillTabs<T extends string>({
  tabs,
  activeId,
  onChange,
  className = '',
  pillColor = 'from-cyan-500 via-indigo-500 to-blue-600',
  layoutId = 'stretchy-pill-active',
}: StretchyPillTabsProps<T>) {
  const currentIndex = tabs.findIndex((t) => t.id === activeId);
  const [prev, setPrev] = useState(currentIndex);
  const [isMovingRight, setIsMovingRight] = useState(true);

  if (prev !== currentIndex) {
    setIsMovingRight(currentIndex > prev);
    setPrev(currentIndex);
  }

  return (
    <div
      className={`relative inline-flex flex-wrap items-center justify-center p-1.5 rounded-full bg-slate-950/95 border border-slate-800 shadow-[0_8px_30px_rgb(0,0,0,0.6)] backdrop-blur-xl ${className}`}
    >
      {tabs.map((tab) => {
        const isSelected = activeId === tab.id;
        const Icon = tab.icon;

        return (
          <button
            key={tab.id}
            onClick={() => onChange(tab.id)}
            className={`relative z-10 flex items-center gap-2 px-5 sm:px-7 py-2.5 rounded-full font-semibold text-xs sm:text-sm transition-colors duration-150 focus:outline-none select-none ${
              isSelected ? 'text-white' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            {/* Pure Seamless Stretchy Pill - Critically Damped (Zero Sluggishness, Zero Bounce, Real Directional Stretch) */}
            {isSelected && (
              <motion.div
                layoutId={layoutId}
                layout
                style={{
                  transformOrigin: isMovingRight ? 'left center' : 'right center',
                }}
                transition={{
                  type: 'spring',
                  stiffness: 600,
                  damping: 38,
                  mass: 0.4,
                }}
                className={`absolute inset-0 rounded-full bg-gradient-to-r ${pillColor} shadow-[0_0_20px_rgba(6,182,212,0.35)] -z-10`}
              />
            )}

            {/* Tab Label & Icon */}
            <span className="relative z-10 flex items-center gap-2">
              {Icon && (
                <Icon
                  className={`w-4 h-4 transition-transform duration-150 ${
                    isSelected ? 'text-white stroke-[2.5]' : 'text-slate-400'
                  }`}
                />
              )}
              <span className="tracking-wide font-medium">{tab.label}</span>
              {tab.badge && (
                <span
                  className={`ml-1 text-[10px] px-2 py-0.5 rounded-full uppercase tracking-wider font-mono font-bold ${
                    isSelected
                      ? 'bg-black/25 text-cyan-200 border border-white/20'
                      : 'bg-slate-900 text-slate-400 border border-slate-800'
                  }`}
                >
                  {tab.badge}
                </span>
              )}
            </span>
          </button>
        );
      })}
    </div>
  );
}
