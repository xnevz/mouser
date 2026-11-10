'use client';

import React, { useRef, useEffect, useState, useCallback } from 'react';
import { motion, useMotionValue, useSpring, useTransform } from 'framer-motion';

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
}

export function StretchyPillTabs<T extends string>({
  tabs,
  activeId,
  onChange,
  className = '',
  pillColor = 'from-cyan-500 via-indigo-500 to-purple-600',
}: StretchyPillTabsProps<T>) {
  const containerRef = useRef<HTMLDivElement>(null);
  const tabRefs = useRef<Map<T, HTMLButtonElement>>(new Map());
  const prevIndexRef = useRef<number>(tabs.findIndex((t) => t.id === activeId));
  const stretchTimerRef = useRef<NodeJS.Timeout | null>(null);

  // Motion values for left and right edges of the pill
  const leftEdgeMV = useMotionValue(0);
  const rightEdgeMV = useMotionValue(0);

  // High-fidelity spring physics with slight elastic inertia
  const springConfig = { stiffness: 440, damping: 25, mass: 0.7 };
  const leftSpring = useSpring(leftEdgeMV, springConfig);
  const rightSpring = useSpring(rightEdgeMV, springConfig);

  // Calculate dynamic width: rightEdge - leftEdge
  const pillWidth = useTransform([leftSpring, rightSpring], ([l, r]) => {
    const width = (r as number) - (l as number);
    return Math.max(width, 24);
  });

  const [isReady, setIsReady] = useState(false);

  // Measure tab dimensions and trigger directional stretch-and-collapse
  const updatePill = useCallback(
    (targetId: T, immediate = false) => {
      const container = containerRef.current;
      const tabEl = tabRefs.current.get(targetId);
      if (!container || !tabEl) return;

      const containerRect = container.getBoundingClientRect();
      const tabRect = tabEl.getBoundingClientRect();

      const targetLeft = tabRect.left - containerRect.left;
      const targetRight = targetLeft + tabRect.width;

      const newIndex = tabs.findIndex((t) => t.id === targetId);
      const prevIndex = prevIndexRef.current;
      prevIndexRef.current = newIndex;

      if (stretchTimerRef.current) {
        clearTimeout(stretchTimerRef.current);
      }

      if (immediate) {
        leftEdgeMV.set(targetLeft);
        rightEdgeMV.set(targetRight);
        leftSpring.jump(targetLeft);
        rightSpring.jump(targetRight);
        return;
      }

      if (newIndex > prevIndex) {
        // Moving right: Over-extend right edge forward (stretch), then collapse to target
        const stretchBoost = Math.min((newIndex - prevIndex) * 18, 48);
        rightEdgeMV.set(targetRight + stretchBoost);
        leftEdgeMV.set(targetLeft);

        stretchTimerRef.current = setTimeout(() => {
          rightEdgeMV.set(targetRight);
        }, 75);
      } else if (newIndex < prevIndex) {
        // Moving left: Over-extend left edge backward (stretch), then collapse to target
        const stretchBoost = Math.min((prevIndex - newIndex) * 18, 48);
        leftEdgeMV.set(targetLeft - stretchBoost);
        rightEdgeMV.set(targetRight);

        stretchTimerRef.current = setTimeout(() => {
          leftEdgeMV.set(targetLeft);
        }, 75);
      } else {
        leftEdgeMV.set(targetLeft);
        rightEdgeMV.set(targetRight);
      }
    },
    [tabs, leftEdgeMV, rightEdgeMV, leftSpring, rightSpring]
  );

  useEffect(() => {
    // Initial mount layout measurement
    const frameId = requestAnimationFrame(() => {
      updatePill(activeId, true);
      setIsReady(true);
    });
    return () => cancelAnimationFrame(frameId);
  }, [activeId, updatePill]);

  useEffect(() => {
    if (isReady) {
      updatePill(activeId, false);
    }
  }, [activeId, isReady, updatePill]);

  // Recalculate on window resize
  useEffect(() => {
    const handleResize = () => updatePill(activeId, true);
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [activeId, updatePill]);

  return (
    <div
      ref={containerRef}
      className={`relative inline-flex flex-wrap items-center justify-center p-1.5 rounded-full bg-slate-950/90 border border-slate-800 shadow-2xl backdrop-blur-xl ${className}`}
    >
      {/* Elastic Stretchy Pill */}
      {isReady && (
        <motion.div
          style={{
            left: leftSpring,
            width: pillWidth,
          }}
          className={`absolute top-1.5 bottom-1.5 rounded-full bg-gradient-to-r ${pillColor} shadow-lg shadow-cyan-500/25 pointer-events-none z-0`}
        >
          {/* Subtle glossy top highlight */}
          <div className="absolute inset-x-3 top-0 h-[1.5px] bg-white/40 rounded-full" />
        </motion.div>
      )}

      {/* Tabs */}
      {tabs.map((tab) => {
        const isSelected = activeId === tab.id;
        const Icon = tab.icon;

        return (
          <button
            key={tab.id}
            ref={(el) => {
              if (el) tabRefs.current.set(tab.id, el);
              else tabRefs.current.delete(tab.id);
            }}
            onClick={() => onChange(tab.id)}
            className={`relative z-10 flex items-center gap-2 px-5 sm:px-7 py-2.5 rounded-full font-semibold text-xs sm:text-sm transition-colors duration-200 focus:outline-none select-none ${
              isSelected ? 'text-white' : 'text-slate-400 hover:text-slate-200'
            }`}
          >
            {Icon && (
              <Icon
                className={`w-4 h-4 transition-transform duration-200 ${
                  isSelected ? 'scale-110 text-white' : 'text-slate-400'
                }`}
              />
            )}
            <span>{tab.label}</span>
            {tab.badge && (
              <span
                className={`ml-1 text-[10px] px-1.5 py-0.5 rounded-full uppercase tracking-wider font-mono ${
                  isSelected
                    ? 'bg-black/30 text-white border border-white/20'
                    : 'bg-slate-800 text-slate-400 border border-slate-700'
                }`}
              >
                {tab.badge}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}
