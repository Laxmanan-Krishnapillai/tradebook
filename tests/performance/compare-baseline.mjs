import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';
export function compare(baseline, run, scenario) {
  const expected = baseline.scenarios?.[scenario];
  if (!expected || expected.throughputReqPerSec == null || expected.p99Ms == null) throw new Error(`No recorded baseline for '${scenario}'. Record one on the documented reference machine and commit baseline.json.`);
  if (typeof run.throughputReqPerSec !== 'number' || typeof run.p99Ms !== 'number') throw new Error(`Run summary for '${scenario}' is incomplete.`);
  const failures = [];
  if (run.throughputReqPerSec < expected.throughputReqPerSec * 0.8) failures.push(`throughput ${run.throughputReqPerSec}/s < 80% of baseline ${expected.throughputReqPerSec}/s`);
  if (run.p99Ms > expected.p99Ms * 1.2) failures.push(`p99 ${run.p99Ms}ms > 120% of baseline ${expected.p99Ms}ms`);
  if (failures.length) throw new Error(`REGRESSION vs baseline: ${failures.join('; ')}`);
}
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const scenario = process.argv[2]; if (!scenario) throw new Error('Usage: node compare-baseline.mjs <scenario>');
  const baseline = JSON.parse(readFileSync(new URL('./baseline.json', import.meta.url), 'utf8'));
  const run = JSON.parse(readFileSync(new URL(`./last-run.${scenario}.json`, import.meta.url), 'utf8'));
  try { compare(baseline, run, scenario); console.log(`'${scenario}' is within the D10 baseline band.`); } catch (error) { console.error(error.message); process.exitCode = 1; }
}
