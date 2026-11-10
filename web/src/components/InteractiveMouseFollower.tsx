'use client';

import React, { useEffect, useState } from 'react';
import { motion, useSpring, useMotionValue } from 'framer-motion';
import { useTheme } from '@/context/ThemeContext';

export const InteractiveMouseFollower = () => {
  const { theme } = useTheme();
  const [isVisible, setIsVisible] = useState(false);
  const [isHovering, setIsHovering] = useState(false);
  const [isClicking, setIsClicking] = useState(false);
  const [isOverHeader, setIsOverHeader] = useState(false);

  // Exact raw client cursor coordinates (zero lag for the focal cursor tip)
  const mouseX = useMotionValue(-100);
  const mouseY = useMotionValue(-100);

  // Smooth elastic trailing halo ring
  const ringSpring = { damping: 26, stiffness: 420, mass: 0.15 };
  const ringX = useSpring(mouseX, ringSpring);
  const ringY = useSpring(mouseY, ringSpring);

  // Fluid ambient bloom trailing with gentle atmosphere
  const bloomSpring = { damping: 32, stiffness: 120, mass: 0.8 };
  const ambientX = useSpring(mouseX, bloomSpring);
  const ambientY = useSpring(mouseY, bloomSpring);

  useEffect(() => {
    // Only activate for desktop mouse / fine pointer devices
    const isPointerFine = window.matchMedia('(pointer: fine)').matches;
    if (!isPointerFine) return;

    const handleMouseMove = (e: MouseEvent) => {
      mouseX.set(e.clientX);
      mouseY.set(e.clientY);
      if (!isVisible) setIsVisible(true);

      const target = e.target as HTMLElement | null;
      if (target) {
        // Detect if hovering over clickable or interactive elements
        const clickable = target.closest('a, button, [role="button"], input, select, textarea');
        setIsHovering(Boolean(clickable));

        // Detect if hovering over the header / navigation bar
        const header = target.closest('header, nav');
        setIsOverHeader(Boolean(header));
      } else {
        setIsHovering(false);
        setIsOverHeader(false);
      }
    };

    const handleMouseDown = () => setIsClicking(true);
    const handleMouseUp = () => setIsClicking(false);
    const handleMouseLeave = () => setIsVisible(false);
    const handleMouseEnter = () => setIsVisible(true);

    window.addEventListener('mousemove', handleMouseMove, { passive: true });
    window.addEventListener('mousedown', handleMouseDown);
    window.addEventListener('mouseup', handleMouseUp);
    document.addEventListener('mouseleave', handleMouseLeave);
    document.addEventListener('mouseenter', handleMouseEnter);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mousedown', handleMouseDown);
      window.removeEventListener('mouseup', handleMouseUp);
      document.removeEventListener('mouseleave', handleMouseLeave);
      document.removeEventListener('mouseenter', handleMouseEnter);
    };
  }, [mouseX, mouseY, isVisible]);

  if (!isVisible) return null;

  return (
    <div className="pointer-events-none fixed inset-0 z-[9999] overflow-hidden select-none">
      {/* 1. Ambient Glow Bloom */}
      <motion.div
        style={{
          x: ambientX,
          y: ambientY,
        }}
        className={`absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-[360px] h-[360px] rounded-full blur-[80px] transition-colors duration-300 ${
          theme === 'dark'
            ? 'bg-gradient-to-tr from-cyan-500/18 via-indigo-500/12 to-fuchsia-500/10'
            : 'bg-gradient-to-tr from-cyan-500/10 via-indigo-500/8 to-fuchsia-500/6'
        }`}
      />

      {/* 2. Elastic Trailing Halo Ring (expands on hover, compresses on click) */}
      <motion.div
        style={{
          x: ringX,
          y: ringY,
        }}
        animate={{
          scale: isClicking ? 0.75 : isHovering ? 1.6 : isOverHeader ? 1.25 : 1,
          borderColor: isHovering
            ? 'rgba(6, 182, 212, 0.9)'
            : isOverHeader
            ? 'rgba(168, 85, 247, 0.8)'
            : 'rgba(6, 182, 212, 0.45)',
          backgroundColor: isHovering
            ? 'rgba(6, 182, 212, 0.16)'
            : isOverHeader
            ? 'rgba(168, 85, 247, 0.12)'
            : 'rgba(6, 182, 212, 0.05)',
        }}
        transition={{ type: 'spring', stiffness: 450, damping: 25 }}
        className="absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-8 h-8 rounded-full border border-cyan-400/40 backdrop-blur-[1px] shadow-[0_0_22px_rgba(6,182,212,0.35)] flex items-center justify-center"
      />

      {/* 3. High-Precision Modern Mouse Arrow (Zero Lag, Layered Over Everything) */}
      <motion.div
        style={{
          x: mouseX,
          y: mouseY,
        }}
        className="absolute top-0 left-0"
      >
        <div
          className={`transform transition-transform duration-100 origin-top-left ${
            isClicking ? 'scale-90' : isHovering ? 'scale-115' : 'scale-100'
          }`}
        >
          {/* Hardware Precision Pointer Arrow */}
          <svg
            width="26"
            height="26"
            viewBox="0 0 24 24"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
            className="drop-shadow-[0_2px_10px_rgba(6,182,212,0.7)]"
          >
            {/* Outer dark halo outline for high contrast on light/blurred header */}
            <path
              d="M0 0 L7.5 19 L11.2 11.8 L18.8 8.2 Z"
              fill="#030712"
              stroke="#030712"
              strokeWidth="2.5"
              strokeLinejoin="round"
            />
            {/* Inner vibrant gradient pointer */}
            <path
              d="M0 0 L7.5 19 L11.2 11.8 L18.8 8.2 Z"
              fill="url(#mouserCursorGrad)"
              stroke={isOverHeader ? '#c084fc' : '#22d3ee'}
              strokeWidth="1.2"
              strokeLinejoin="round"
            />
            <defs>
              <linearGradient id="mouserCursorGrad" x1="0" y1="0" x2="19" y2="19" gradientUnits="userSpaceOnUse">
                <stop stopColor="#ffffff" />
                <stop offset="0.45" stopColor={isOverHeader ? '#e9d5ff' : '#a5f3fc'} />
                <stop offset="1" stopColor={isOverHeader ? '#a855f7' : '#06b6d4'} />
              </linearGradient>
            </defs>
          </svg>

          {/* Tiny focal tip glow */}
          <span className="absolute top-0 left-0 -translate-x-[1px] -translate-y-[1px] w-1.5 h-1.5 rounded-full bg-white shadow-[0_0_8px_#38bdf8]" />
        </div>
      </motion.div>
    </div>
  );
};
