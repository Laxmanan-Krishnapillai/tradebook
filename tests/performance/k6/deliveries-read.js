import http from 'k6/http';
import { check } from 'k6';
const BASE_URL = __ENV.BASE_URL || 'http://127.0.0.1:5000'; const PROFILE = __ENV.PROFILE || 'smoke';
export const options = { scenarios: { load: { executor: 'constant-vus', vus: Number(__ENV.VUS || 10), duration: PROFILE === 'sustained' ? '10m' : '60s' } }, summaryTrendStats: ['avg','p(95)','p(99)'], thresholds: { http_req_failed: ['rate==0'], checks: ['rate==1'] } };
export function setup() { if (!__ENV.API_JWT) throw new Error('API_JWT is required'); return { token: __ENV.API_JWT }; }
export default function (data) { const response = http.get(`${BASE_URL}/api/v1/deliveries?page=1&pageSize=100`, { headers: { Authorization: `Bearer ${data.token}` } }); check(response, { 'read succeeds (200)': r => r.status === 200 }); }
export function handleSummary(data) { return { './last-run.deliveries-read.json': JSON.stringify({ scenario: 'deliveries-read', throughputReqPerSec: data.metrics.http_reqs.values.rate, p99Ms: data.metrics.http_req_duration.values['p(99)'] }, null, 2) }; }
