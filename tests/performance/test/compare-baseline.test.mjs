import assert from 'node:assert/strict'; import test from 'node:test'; import { compare } from '../compare-baseline.mjs';
const baseline = { scenarios: { read: { throughputReqPerSec: 100, p99Ms: 100 } } };
test('accepts boundary and healthy measurements', () => { assert.doesNotThrow(() => compare(baseline, { throughputReqPerSec: 80, p99Ms: 120 }, 'read')); });
test('rejects throughput regression greater than twenty percent', () => { assert.throws(() => compare(baseline, { throughputReqPerSec: 79.99, p99Ms: 100 }, 'read'), /throughput/); });
test('rejects p99 regression greater than twenty percent', () => { assert.throws(() => compare(baseline, { throughputReqPerSec: 100, p99Ms: 120.01 }, 'read'), /p99/); });
test('refuses an unrecorded baseline', () => { assert.throws(() => compare({ scenarios: { read: { throughputReqPerSec: null, p99Ms: null } } }, { throughputReqPerSec: 1, p99Ms: 1 }, 'read'), /No recorded baseline/); });
