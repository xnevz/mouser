'use client';

import React, { createContext, useContext, useEffect, useState } from 'react';

export type Theme = 'system' | 'light' | 'dark';
export type ResolvedTheme = 'light' | 'dark';

interface ThemeContextType {
  theme: Theme;
  resolvedTheme: ResolvedTheme;
  toggleTheme: () => void;
  setTheme: (theme: Theme) => void;
}

const ThemeContext = createContext<ThemeContextType | undefined>(undefined);

function applyDomTheme(resolvedTheme: ResolvedTheme) {
  if (typeof document === 'undefined') return;
  const root = document.documentElement;
  if (resolvedTheme === 'dark') {
    root.classList.add('dark');
    root.classList.remove('light');
    root.setAttribute('data-theme', 'dark');
    root.style.colorScheme = 'dark';
  } else {
    root.classList.remove('dark');
    root.classList.add('light');
    root.setAttribute('data-theme', 'light');
    root.style.colorScheme = 'light';
  }
}

export const ThemeProvider = ({ children }: { children: React.ReactNode }) => {
  const [theme, setThemeState] = useState<Theme>('system');
  const [resolvedTheme, setResolvedTheme] = useState<ResolvedTheme>('dark');

  useEffect(() => {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');

    requestAnimationFrame(() => {
      const savedTheme = localStorage.getItem('mouser-theme') as Theme | null;
      if (savedTheme === 'light' || savedTheme === 'dark') {
        setThemeState(savedTheme);
        setResolvedTheme(savedTheme);
        applyDomTheme(savedTheme);
      } else {
        setThemeState('system');
        const res: ResolvedTheme = mediaQuery.matches ? 'dark' : 'light';
        setResolvedTheme(res);
        applyDomTheme(res);
      }
    });

    const handleSystemChange = (e: MediaQueryListEvent) => {
      const currentStored = localStorage.getItem('mouser-theme');
      if (!currentStored || currentStored === 'system') {
        const nextResolved: ResolvedTheme = e.matches ? 'dark' : 'light';
        setResolvedTheme(nextResolved);
        applyDomTheme(nextResolved);
      }
    };

    mediaQuery.addEventListener('change', handleSystemChange);
    return () => mediaQuery.removeEventListener('change', handleSystemChange);
  }, []);

  const setTheme = (newTheme: Theme) => {
    setThemeState(newTheme);
    if (newTheme === 'system') {
      localStorage.removeItem('mouser-theme');
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
      const res: ResolvedTheme = prefersDark ? 'dark' : 'light';
      setResolvedTheme(res);
      applyDomTheme(res);
    } else {
      localStorage.setItem('mouser-theme', newTheme);
      setResolvedTheme(newTheme);
      applyDomTheme(newTheme);
    }
  };

  const toggleTheme = () => {
    if (theme === 'system') {
      setTheme('light');
    } else if (theme === 'light') {
      setTheme('dark');
    } else {
      setTheme('system');
    }
  };

  return (
    <ThemeContext.Provider value={{ theme, resolvedTheme, toggleTheme, setTheme }}>
      {children}
    </ThemeContext.Provider>
  );
};

export const useTheme = () => {
  const context = useContext(ThemeContext);
  if (!context) {
    throw new Error('useTheme must be used within a ThemeProvider');
  }
  return context;
};

