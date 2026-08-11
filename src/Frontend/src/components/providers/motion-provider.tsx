import { LazyMotion, MotionConfig, domAnimation } from 'motion/react';
import { useEffect, type PropsWithChildren } from 'react';
import { motionTokens } from '../../lib/motion/tokens';
import { usePreferences } from '../../stores/preferences';

export function MotionProvider({ children }: PropsWithChildren) {
  const reduceMotion = usePreferences((state) => state.reduceMotion);
  const theme = usePreferences((state) => state.theme);

  useEffect(() => {
    const root = document.documentElement;
    const systemTheme = window.matchMedia('(prefers-color-scheme: dark)');
    const applyTheme = () => root.classList.toggle('dark', theme === 'dark' || (theme === 'system' && systemTheme.matches));
    applyTheme();
    if (theme === 'system') systemTheme.addEventListener?.('change', applyTheme);
    return () => systemTheme.removeEventListener?.('change', applyTheme);
  }, [theme]);

  useEffect(() => {
    const root = document.documentElement;
    root.classList.toggle('reduce-motion', reduceMotion);
  }, [reduceMotion]);

  return (
    <LazyMotion features={domAnimation} strict>
      <MotionConfig reducedMotion={reduceMotion ? 'always' : 'user'} transition={{ duration: motionTokens.duration.base, ease: motionTokens.ease.standard }}>
        {children}
      </MotionConfig>
    </LazyMotion>
  );
}
