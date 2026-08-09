import http from 'k6/http';
import { check } from 'k6';
import exec from 'k6/execution';
import crypto from 'k6/crypto';
const BASE_URL = __ENV.BASE_URL || 'http://127.0.0.1:5000';
const PROFILE = __ENV.PROFILE || 'smoke';
export const options = { scenarios: { load: { executor: 'constant-vus', vus: Number(__ENV.VUS || 10), duration: PROFILE === 'sustained' ? '10m' : '60s' } }, summaryTrendStats: ['avg','p(95)','p(99)'], thresholds: { http_req_failed: ['rate==0'], checks: ['rate==1'] } };
export function setup() {
  if (!__ENV.API_JWT || !__ENV.E2E_CONTRACT_ID) throw new Error('API_JWT and E2E_CONTRACT_ID are required');
  return { token: __ENV.API_JWT, contractId: __ENV.E2E_CONTRACT_ID };
}
export default function (data) {
  const iteration = exec.scenario.iterationInTest;
  const digest = crypto.md5(`task09-contract-${(iteration % 100000) + 1}`, 'hex');
  const contractId = `${digest.slice(0, 8)}-${digest.slice(8, 12)}-4${digest.slice(13, 16)}-8${digest.slice(17, 20)}-${digest.slice(20, 32)}`;
  const monthIndex = 3 + Math.floor(iteration / 100000);
  const year = 2026 + Math.floor(monthIndex / 12);
  const month = String((monthIndex % 12) + 1).padStart(2, '0');
  if (year > 9999) throw new Error('Scenario exhausted valid PostgreSQL date months');
  const instance = `K6.${exec.vu.idInTest}.${iteration}.${Date.now()}`;
  const response = http.post(`${BASE_URL}/api/v1/deliveries`, JSON.stringify({ contractId, contractInstanceId: instance, bookType: 'Sourcing', supplyMonth: `${year}-${month}-01`, volumeNominatedMwh: 12000, volumeRealisedMwh: 11840 }), { headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${data.token}` } });
  check(response, { 'created (201)': r => r.status === 201 });
}
export function handleSummary(data) { return { './last-run.api-delivery-ingestion.json': JSON.stringify({ scenario: 'api-delivery-ingestion', throughputReqPerSec: data.metrics.http_reqs.values.rate, p99Ms: data.metrics.http_req_duration.values['p(99)'] }, null, 2) }; }
