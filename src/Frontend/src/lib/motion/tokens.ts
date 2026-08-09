export const motionTokens = {
  duration: { instant: 0.075, fast: 0.1, base: 0.15, moderate: 0.2 },
  ease: {
    standard: [0.2, 0, 0, 1],
    decelerate: [0, 0, 0, 1],
    accelerate: [0.3, 0, 1, 1],
    swift: [0.16, 1, 0.3, 1],
  },
} as const;
