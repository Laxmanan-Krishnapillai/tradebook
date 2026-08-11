import { usePreferences, type Density } from '../../stores/preferences';
import { Button } from './button';

const densities: readonly Density[] = ['condensed', 'regular', 'relaxed'];

export function DensityToggle() {
  const density = usePreferences((state) => state.density);
  const setDensity = usePreferences((state) => state.setDensity);
  const reduceMotion = usePreferences((state) => state.reduceMotion);
  const setReduceMotion = usePreferences((state) => state.setReduceMotion);

  return (
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
  );
}
