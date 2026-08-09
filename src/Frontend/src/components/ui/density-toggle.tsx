import { usePreferences, type Density } from '../../stores/preferences';

const densities: readonly Density[] = ['condensed', 'regular', 'relaxed'];

export function DensityToggle() {
  const density = usePreferences((state) => state.density);
  const setDensity = usePreferences((state) => state.setDensity);
  const reduceMotion = usePreferences((state) => state.reduceMotion);
  const setReduceMotion = usePreferences((state) => state.setReduceMotion);

  return (
    <fieldset className="flex gap-1" aria-label="Table density">
      {densities.map((option) => (
        <button className="secondary capitalize" aria-pressed={density === option} key={option} onClick={() => setDensity(option)} type="button">
          {option}
        </button>
      ))}
      <button className="secondary" aria-pressed={reduceMotion} onClick={() => setReduceMotion(!reduceMotion)} type="button">
        Reduce motion
      </button>
    </fieldset>
  );
}
