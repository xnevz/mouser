'use client';

import React, { useEffect, useState } from 'react';
import { motion, useSpring, useMotionValue } from 'framer-motion';

export const InteractiveMouseFollower = () => {
  const [isVisible, setIsVisible] = useState(false);

  // Exact client cursor coordinates
  const mouseX = useMotionValue(-200);
  const mouseY = useMotionValue(-200);

  // Snappy responsive spring for center focal ring
  const centerSpring = { damping: 28, stiffness: 450, mass: 0.1 };
  const cursorX = useSpring(mouseX, centerSpring);
  const cursorY = useSpring(mouseY, centerSpring);

  // Fluid ambient bloom trailing with gentle inertia
  const bloomSpring = { damping: 32, stiffness: 140, mass: 0.6 };
  const ambientX = useSpring(mouseX, bloomSpring);
  const ambientY = useSpring(mouseY, bloomSpring);

  useEffect(() => {
    // Only activate for real mouse / fine pointer devices
    const isPointerFine = window.matchMedia('(pointer: fine)').matches;
    if (!isPointerFine) return;

    const handleMouseMove = (e: MouseEvent) => {
      mouseX.set(e.clientX);
      mouseY.set(e.clientY);
      if (!isVisible) setIsVisible(true);
    };

    const handleMouseLeave = () => {
      setIsVisible(false);
    };

    window.addEventListener('mousemove', handleMouseMove, { passive: true });
    document.addEventListener('mouseleave', handleMouseLeave);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseleave', handleMouseLeave);
    };
  }, [mouseX, mouseY, isVisible]);

  if (!isVisible) return null;

  return (
    <div className="pointer-events-none fixed inset-0 z-30 overflow-hidden">
      {/* Outer ambient glow mesh orb - positioned absolutely at top-0 left-0 with centered translation */}
      <motion.div
        style={{
          x: ambientX,
          y: ambientY,
        }}
        className="absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-[420px] h-[420px] rounded-full bg-gradient-to-tr from-cyan-500/15 via-indigo-500/12 to-fuchsia-500/10 blur-[90px]"
      />

      {/* High-precision interactive cursor halo - perfectly centered right under the mouse tip */}
      <motion.div
        style={{
          x: cursorX,
          y: cursorY,
        }}
        className="absolute top-0 left-0 -translate-x-1/2 -translate-y-1/2 w-8 h-8 rounded-full border border-cyan-400/40 bg-cyan-400/5 shadow-[0_0_20px_rgba(6,182,212,0.35)] backdrop-blur-[1px] flex items-center justify-center"
      >
        <div className="w-1.5 h-1.5 rounded-full bg-cyan-300 shadow-[0_0_8px_#22d3ee]" />
      </motion.div>
    </div>
  );
};
