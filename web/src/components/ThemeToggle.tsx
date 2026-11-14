'use client';

import React from 'react';
import { useTheme } from '@/context/ThemeContext';
import { Sun, Moon, Monitor } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';

export const ThemeToggle = ({ className = '' }: { className?: string }) => {
  const { theme, toggleTheme } = useTheme();

  const getLabel = () => {
    switch (theme) {
      case 'system':
        return 'Theme: System preference. Click to switch to Light mode';
      case 'light':
        return 'Theme: Light mode. Click to switch to Dark mode';
      case 'dark':
        return 'Theme: Dark mode. Click to switch to System mode';
    }
  };

  const getTitle = () => {
    switch (theme) {
      case 'system':
        return 'Theme: System (click for Light mode)';
      case 'light':
        return 'Theme: Light (click for Dark mode)';
      case 'dark':
        return 'Theme: Dark (click for System mode)';
    }
  };

  return (
    <button
      onClick={toggleTheme}
      aria-label={getLabel()}
      title={getTitle()}
      className={`relative p-2 rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-cyan-500/50 ${
        theme === 'dark'
          ? 'bg-slate-900/90 hover:bg-slate-800 text-amber-400 border border-slate-800 shadow-md shadow-black/30'
          : theme === 'light'
          ? 'bg-slate-100 hover:bg-slate-200 text-indigo-600 border border-slate-300 shadow-sm'
          : 'bg-slate-800/80 hover:bg-slate-700/80 text-cyan-400 border border-slate-700 shadow-sm'
      } ${className}`}
    >
      <AnimatePresence mode="wait" initial={false}>
        {theme === 'system' ? (
          <motion.div
            key="system"
            initial={{ rotate: -90, opacity: 0, scale: 0.7 }}
            animate={{ rotate: 0, opacity: 1, scale: 1 }}
            exit={{ rotate: 90, opacity: 0, scale: 0.7 }}
            transition={{ duration: 0.2 }}
            className="flex items-center justify-center"
          >
            <Monitor className="w-4 h-4 text-cyan-400" />
          </motion.div>
        ) : theme === 'dark' ? (
          <motion.div
            key="moon"
            initial={{ rotate: -90, opacity: 0, scale: 0.7 }}
            animate={{ rotate: 0, opacity: 1, scale: 1 }}
            exit={{ rotate: 90, opacity: 0, scale: 0.7 }}
            transition={{ duration: 0.2 }}
            className="flex items-center justify-center"
          >
            <Moon className="w-4 h-4 fill-amber-400/20 text-amber-400" />
          </motion.div>
        ) : (
          <motion.div
            key="sun"
            initial={{ rotate: 90, opacity: 0, scale: 0.7 }}
            animate={{ rotate: 0, opacity: 1, scale: 1 }}
            exit={{ rotate: -90, opacity: 0, scale: 0.7 }}
            transition={{ duration: 0.2 }}
            className="flex items-center justify-center"
          >
            <Sun className="w-4 h-4 text-amber-500 fill-amber-400" />
          </motion.div>
        )}
      </AnimatePresence>
    </button>
  );
};
