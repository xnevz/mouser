'use client';

import React, { useState, useEffect } from 'react';
import { MousePointer, Github, Download, Menu, X, Sparkles, Terminal } from 'lucide-react';

export const Navbar = () => {
  const [scrolled, setScrolled] = useState(false);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  useEffect(() => {
    const handleScroll = () => {
      setScrolled(window.scrollY > 20);
    };
    window.addEventListener('scroll', handleScroll);
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <header
      className={`fixed top-0 left-0 right-0 z-50 transition-all duration-300 ${
        scrolled
          ? 'bg-slate-950/80 backdrop-blur-xl border-b border-slate-800/80 shadow-lg shadow-black/40'
          : 'bg-transparent border-b border-transparent'
      }`}
    >
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16 sm:h-20">
          {/* Logo & Brand */}
          <a href="#" className="flex items-center gap-3 group">
            <div className="relative flex items-center justify-center w-10 h-10 rounded-xl bg-gradient-to-tr from-cyan-500 via-indigo-500 to-purple-600 p-[1.5px] shadow-lg shadow-cyan-500/20 group-hover:shadow-cyan-500/40 transition-shadow">
              <div className="w-full h-full bg-slate-950 rounded-[10px] flex items-center justify-center">
                <MousePointer className="w-5 h-5 text-cyan-400 group-hover:scale-110 transition-transform" />
              </div>
            </div>
            <div className="flex flex-col">
              <div className="flex items-center gap-2">
                <span className="font-extrabold text-xl tracking-tight text-white group-hover:text-cyan-300 transition-colors">
                  Mouser
                </span>
                <span className="px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wider rounded-full bg-cyan-500/10 text-cyan-400 border border-cyan-500/20">
                  v1.0-rc.3
                </span>
              </div>
              <span className="text-[11px] text-slate-400 font-mono hidden sm:block">
                Virtual KVM & Smart Clipboard
              </span>
            </div>
          </a>

          {/* Desktop Navigation */}
          <nav className="hidden md:flex items-center gap-8">
            <a
              href="#demo"
              className="text-sm font-medium text-slate-300 hover:text-cyan-400 transition-colors flex items-center gap-1.5"
            >
              <Sparkles className="w-4 h-4 text-cyan-400" />
              Interactive Demo
            </a>
            <a
              href="#downloads"
              className="text-sm font-medium text-slate-300 hover:text-cyan-400 transition-colors"
            >
              Downloads
            </a>
            <a
              href="#features"
              className="text-sm font-medium text-slate-300 hover:text-cyan-400 transition-colors"
            >
              Features
            </a>
            <a
              href="#architecture"
              className="text-sm font-medium text-slate-300 hover:text-cyan-400 transition-colors"
            >
              Architecture
            </a>
            <a
              href="#comparison"
              className="text-sm font-medium text-slate-300 hover:text-cyan-400 transition-colors"
            >
              Comparison
            </a>
          </nav>

          {/* Action CTAs */}
          <div className="hidden sm:flex items-center gap-3">
            <a
              href="https://github.com/mouser-org/mouser"
              target="_blank"
              rel="noreferrer"
              className="flex items-center gap-2 px-3.5 py-2 rounded-lg text-sm font-medium text-slate-300 bg-slate-900/80 hover:bg-slate-800 border border-slate-700/80 hover:border-slate-600 transition-all hover:text-white"
            >
              <Github className="w-4 h-4" />
              <span>GitHub</span>
              <span className="text-xs bg-slate-800 px-1.5 py-0.5 rounded text-slate-400 font-mono">
                ★ 2.4k
              </span>
            </a>

            <a
              href="#downloads"
              className="relative inline-flex items-center justify-center p-0.5 overflow-hidden text-sm font-medium rounded-lg group bg-gradient-to-r from-cyan-500 to-indigo-600 group-hover:from-cyan-500 group-hover:to-indigo-600 hover:shadow-lg hover:shadow-cyan-500/25 transition-all"
            >
              <span className="relative px-4 py-2 transition-all ease-in duration-75 bg-slate-950 rounded-[6px] group-hover:bg-opacity-0 flex items-center gap-2 text-white font-semibold">
                <Download className="w-4 h-4 text-cyan-400 group-hover:text-white" />
                <span>Get Mouser</span>
              </span>
            </a>
          </div>

          {/* Mobile hamburger */}
          <div className="flex md:hidden items-center gap-2">
            <button
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              className="p-2 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 focus:outline-none"
              aria-label="Toggle menu"
            >
              {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>
      </div>

      {/* Mobile Drawer */}
      {mobileMenuOpen && (
        <div className="md:hidden bg-slate-950/95 backdrop-blur-2xl border-b border-slate-800 px-4 pt-2 pb-6 space-y-3">
          <a
            href="#demo"
            onClick={() => setMobileMenuOpen(false)}
            className="block px-3 py-2 rounded-md text-base font-medium text-slate-300 hover:text-cyan-400 hover:bg-slate-900"
          >
            Interactive Demo
          </a>
          <a
            href="#downloads"
            onClick={() => setMobileMenuOpen(false)}
            className="block px-3 py-2 rounded-md text-base font-medium text-slate-300 hover:text-cyan-400 hover:bg-slate-900"
          >
            Downloads & Installers
          </a>
          <a
            href="#features"
            onClick={() => setMobileMenuOpen(false)}
            className="block px-3 py-2 rounded-md text-base font-medium text-slate-300 hover:text-cyan-400 hover:bg-slate-900"
          >
            Features & Capabilities
          </a>
          <a
            href="#architecture"
            onClick={() => setMobileMenuOpen(false)}
            className="block px-3 py-2 rounded-md text-base font-medium text-slate-300 hover:text-cyan-400 hover:bg-slate-900"
          >
            Architecture
          </a>
          <a
            href="#comparison"
            onClick={() => setMobileMenuOpen(false)}
            className="block px-3 py-2 rounded-md text-base font-medium text-slate-300 hover:text-cyan-400 hover:bg-slate-900"
          >
            Feature Comparison
          </a>
          <div className="pt-2 flex flex-col gap-2">
            <a
              href="#downloads"
              onClick={() => setMobileMenuOpen(false)}
              className="w-full py-2.5 px-4 rounded-lg bg-gradient-to-r from-cyan-500 to-indigo-600 text-white font-medium text-center flex items-center justify-center gap-2"
            >
              <Download className="w-4 h-4" /> Download Mouser Free
            </a>
          </div>
        </div>
      )}
    </header>
  );
};
