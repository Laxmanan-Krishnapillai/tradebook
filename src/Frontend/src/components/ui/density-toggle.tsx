import { usePreferences, type Density, type Theme } from '../../stores/preferences';
import { Button } from './button';

const densities: readonly Density[] = ['condensed', 'regular', 'relaxed'];
const themes: ReadonlyArray<{ label: string; value: Theme }> = [
  { label: 'Light', value: 'light' },
  { label: 'System', value: 'system' },
  { label: 'Dark', value: 'dark' },
];

export function DensityToggle() {
  const density = usePreferences((state) => state.density);
  const setDensity = usePreferences((state) => state.setDensity);
  const reduceMotion = usePreferences((state) => state.reduceMotion);
  const setReduceMotion = usePreferences((state) => state.setReduceMotion);
  const theme = usePreferences((state) => state.theme);
  const setTheme = usePreferences((state) => state.setTheme);

  return (
    <>
      <fieldset data-slot="theme-toggle" aria-label="Appearance">
        {themes.map((option) => (
          <Button intent="ghost" size="sm" aria-pressed={theme === option.value} key={option.value} onClick={() => setTheme(option.value)} type="button">
            {option.label}
          </Button>
        ))}
      </fieldset>
      <fieldset data-slot="density-toggle" aria-label="Table density">
        {densities.map((option) => (
          <Button intent="ghost" size="sm" aria-pressed={density === option} key={option} onClick={() => setDensity(option)} type="button">
            {option}
          </Button>
        ))}
        <Button intent="ghost" size="sm" aria-pressed={reduceMotion} onClick={() => setReduceMotion(!reduceMotion)} type="button">
          Reduce motion
        </Button>
      </fieldset>
    </>
  );
}
