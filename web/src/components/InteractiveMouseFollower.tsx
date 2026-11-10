'use client';

import React, { useEffect, useState } from 'react';
import { motion, useSpring, useMotionValue } from 'framer-motion';

export const InteractiveMouseFollower = () => {
  const [isVisible, setIsVisible] = useState(false);
  const [isHovering, setIsHovering] = useState(false);
  const [isClicking, setIsClicking] = useState(false);

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

      // Check if hovering over clickable elements
      const target = e.target as HTMLElement | null;
      if (target && target.closest('a, button, [role="button"], input, select, textarea')) {
        setIsHovering(true);
      } else {
        setIsHovering(false);
      }
    };

    const handleMouseDown = () => setIsClicking(true);
    const handleMouseUp = () => setIsClicking(false);
    const handleMouseLeave = () => setIsVisible(false);

    window.addEventListener('mousemove', handleMouseMove, { passive: true });
    window.addEventListener('mousedown', handleMouseDown);
    window.addEventListener('mouseup', handleMouseUp);
    document.addEventListener('mouseleave', handleMouseLeave);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      window.removeEventListener('mousedown', handleMouseDown);
      window.removeEventListener('mouseup', handleMouseUp);
      document.removeEventListener('mouseleave', handleMouseLeave);
    };
  }, [mouseX, mouseY, isVisible]);

  if (!isVisible) return null;

  return (
    <div className="pointer-events-none fixed inset-0 z-50 overflow-hidden select-none">
      {/* 1. Ambient Glow Bloom */}
      <motion.div
        style={{
          x: ambientX,
          y: ambientY,
        }}
        className="absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-[380px] h-[380px] rounded-full bg-gradient-to-tr from-cyan-500/15 via-indigo-500/10 to-fuchsia-500/8 blur-[85px]"
      />

      {/* 2. Elastic Trailing Halo Ring (expands on hover, compresses on click) */}
      <motion.div
        style={{
          x: ringX,
          y: ringY,
        }}
        animate={{
          scale: isClicking ? 0.75 : isHovering ? 1.5 : 1,
          borderColor: isHovering ? 'rgba(6, 182, 212, 0.8)' : 'rgba(6, 182, 212, 0.4)',
          backgroundColor: isHovering ? 'rgba(6, 182, 212, 0.12)' : 'rgba(6, 182, 212, 0.04)',
        }}
        transition={{ type: 'spring', stiffness: 450, damping: 25 }}
        className="absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-8 h-8 rounded-full border border-cyan-400/40 backdrop-blur-[1px] shadow-[0_0_20px_rgba(6,182,212,0.3)] flex items-center justify-center"
      />

      {/* 3. High-Precision Cursor Focal Tip (zero lag, exactly where mouse is) */}
      <motion.div
        style={{
          x: mouseX,
          y: mouseY,
        }}
        className="absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-2 h-2 rounded-full bg-cyan-300 shadow-[0_0_10px_#22d3ee,0_0_20px_#06b6d4]"
      />
    </div>
  );
};
