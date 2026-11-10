'use client';

import React, { useEffect, useState } from 'react';
import { motion, useSpring, useMotionValue } from 'framer-motion';

export const InteractiveMouseFollower = () => {
  const [isVisible, setIsVisible] = useState(false);

  // High precision mouse coordinates
  const mouseX = useMotionValue(-100);
  const mouseY = useMotionValue(-100);

  // Smooth bouncy spring physics
  const springConfig = { damping: 25, stiffness: 220, mass: 0.5 };
  const smoothX = useSpring(mouseX, springConfig);
  const smoothY = useSpring(mouseY, springConfig);

  // Secondary trailer orb with more inertia
  const trailerConfig = { damping: 35, stiffness: 120, mass: 1.2 };
  const trailerX = useSpring(mouseX, trailerConfig);
  const trailerY = useSpring(mouseY, trailerConfig);

  useEffect(() => {
    // Only track if on fine pointer device (desktop mouse)
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

    window.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseleave', handleMouseLeave);

    return () => {
      window.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseleave', handleMouseLeave);
    };
  }, [mouseX, mouseY, isVisible]);

  if (!isVisible) return null;

  return (
    <div className="pointer-events-none fixed inset-0 z-40 overflow-hidden transition-opacity duration-500">
      {/* Outer ambient gradient bloom */}
      <motion.div
        style={{
          x: trailerX,
          y: trailerY,
          translateX: '-50%',
          translateY: '-50%',
        }}
        className="w-[360px] h-[360px] rounded-full bg-gradient-to-tr from-cyan-500/12 via-indigo-500/10 to-fuchsia-500/8 blur-[80px]"
      />

      {/* Inner sharp focal aura */}
      <motion.div
        style={{
          x: smoothX,
          y: smoothY,
          translateX: '-50%',
          translateY: '-50%',
        }}
        className="w-10 h-10 rounded-full border border-cyan-400/30 bg-cyan-400/10 backdrop-blur-[2px] shadow-lg shadow-cyan-500/20"
      />
    </div>
  );
};
