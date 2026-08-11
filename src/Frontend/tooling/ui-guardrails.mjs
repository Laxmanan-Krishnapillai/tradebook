import { ESLint } from 'eslint';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const fixture = new URL('../src/features/__guardrail_fixture.tsx', import.meta.url);
const frontendRoot = new URL('../', import.meta.url);
const fixturePath = fileURLToPath(fixture);
const eslint = new ESLint({ cwd: fileURLToPath(frontendRoot) });

mkdirSync(new URL('../src/features/', import.meta.url), { recursive: true });

async function lint(source, expectedRules, shouldPass = false) {
  writeFileSync(fixture, source);
  const results = await eslint.lintFiles([fixturePath]);
  const rules = results.flatMap((result) => result.messages.map((message) => message.ruleId));
  const hasErrors = results.some((result) => result.errorCount > 0);

  if (shouldPass && hasErrors) {
    throw new Error(`Expected lint pass, received: ${JSON.stringify(results)}`);
  }

  if (!shouldPass && !hasErrors) {
    throw new Error(`Expected lint failure for ${expectedRules.join(', ')}`);
  }

  for (const rule of expectedRules) {
    if (!rules.includes(rule)) {
      throw new Error(`Missing ${rule} in ESLint output: ${JSON.stringify(results)}`);
    }
  }
}

try {
  await lint('export const Drift = () => <div className="p-[7px] bg-[#ff0000]" />;\n', ['tailwindcss/no-arbitrary-value']);
  await lint("import { Button } from '@base-ui-components/react/button'; export const Raw = Button;\n", ['boundaries/external']);
  await lint('export const Hatch = () => <div className="u-density-override" />;\n', [], true);
  await lint('export const Custom = () => <div className="invented-class" />;\n', ['tailwindcss/no-custom-classname']);
  await lint("const cva = (x: string) => x; export const variant = cva('p-[7px]');\n", ['tailwindcss/no-arbitrary-value']);
  process.stdout.write('AGUI-01..04 guardrail assertions passed\n');
} finally {
  rmSync(fixture, { force: true });
}
