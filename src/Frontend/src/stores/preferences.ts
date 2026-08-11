import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type Density = 'condensed' | 'regular' | 'relaxed';
export type Theme = 'light' | 'dark' | 'system';

interface PreferencesState {
  density: Density;
  reduceMotion: boolean;
  theme: Theme;
  setDensity: (density: Density) => void;
  setReduceMotion: (reduceMotion: boolean) => void;
  setTheme: (theme: Theme) => void;
}

export const usePreferences = create<PreferencesState>()(
  persist(
    (set) => ({
      density: 'regular',
      reduceMotion: false,
      theme: 'system',
      setDensity: (density) => set({ density }),
      setReduceMotion: (reduceMotion) => set({ reduceMotion }),
      setTheme: (theme) => set({ theme }),
    }),
    { name: 'tradebook-preferences' },
  ),
);
