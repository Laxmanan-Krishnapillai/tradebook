import { execFileSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';

const fixture = new URL('../src/features/__guardrail_fixture.tsx', import.meta.url);
const frontendRoot = new URL('../', import.meta.url);
mkdirSync(new URL('../src/features/', import.meta.url), { recursive: true });

function lint(source, expectedRules, shouldPass = false) {
  writeFileSync(fixture, source);
  try {
    execFileSync('npx', ['eslint', fixture.pathname, '--format', 'json'], { cwd: frontendRoot.pathname, encoding: 'utf8', stdio: 'pipe' });
    if (!shouldPass) throw new Error(`Expected lint failure for ${expectedRules.join(', ')}`);
  } catch (error) {
    if (shouldPass) throw error;
    const output = `${String(error.stdout ?? '')}\n${String(error.stderr ?? '')}`;
    for (const rule of expectedRules) {
      if (!output.includes(rule)) throw new Error(`Missing ${rule} in ESLint output: ${output}`);
    }
  }
}

try {
  lint('export const Drift = () => <div className="p-[7px] bg-[#ff0000]" />;\n', ['tailwindcss/no-arbitrary-value']);
  lint("import { Button } from '@base-ui-components/react/button'; export const Raw = Button;\n", ['boundaries/external']);
  lint('export const Hatch = () => <div className="u-density-override" />;\n', [], true);
  lint('export const Custom = () => <div className="invented-class" />;\n', ['tailwindcss/no-custom-classname']);
  lint("const cva = (x: string) => x; export const variant = cva('p-[7px]');\n", ['tailwindcss/no-arbitrary-value']);
  process.stdout.write('AGUI-01..04 guardrail assertions passed\n');
} finally {
  rmSync(fixture, { force: true });
}
