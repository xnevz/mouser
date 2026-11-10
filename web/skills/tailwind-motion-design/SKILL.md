# Skill: Tailwind CSS v4 & Framer Motion Design Systems

## Overview
Guidelines for creating futuristic, hardware-grade user interfaces with deep dark themes, luminous linear gradients, glassmorphism backdrops, and responsive motion graphics.

---

## Design Tokens & Visual Hierarchy

1. **Color Palette**:
   - Background: Pitch Slate `#060911` with semi-transparent surfaces (`bg-slate-900/80`, `bg-slate-950/90`).
   - Neon Accents: Cyan `#06b6d4`, Indigo `#6366f1`, Fuchsia `#d946ef`, Emerald `#10b981`.
2. **Glow & Gradient Utilities**:
   - `glow-cyan`: Soft diffused cyan glow for interactive primary elements.
   - `bg-radial-gradient`: Atmospheric spotlighting from page apex down through hero components.
   - `bg-grid-pattern`: 40px grid overlay simulating blueprint precision.
3. **Framer Motion Micro-Interactions**:
   - Staggered entrances for list cards (`initial={{ opacity: 0, y: 20 }}`).
   - Spring physics on cursor roaming demonstrations.
   - Toast notifications with `<AnimatePresence>` for non-blocking feedback.
