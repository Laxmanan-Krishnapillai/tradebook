import type { SaveDashboardRequest } from '../../api/generated/types.gen';
import type { SaveDashboardResponse } from '../../api/generated/types.gen';
import type { DashboardSpecification } from '../../types/visualizations';
import { apiFetch } from './client';

function dashboardLayout(value: unknown): DashboardSpecification {
  if (typeof value !== 'object' || value === null || !('dashboardId' in value) || !('version' in value)) {
    throw new TypeError('Dashboard response contains an invalid layout.');
  }
  return value as DashboardSpecification;
}

export async function saveDashboard(dashboard: DashboardSpecification): Promise<DashboardSpecification> {
  const request: SaveDashboardRequest = { dashboardId: dashboard.dashboardId, version: dashboard.version, layout: dashboard };
  const response = await apiFetch<SaveDashboardResponse>(`/api/v1/dashboards/${dashboard.dashboardId}`, { method: 'PUT', body: JSON.stringify(request) });
  return dashboardLayout(response.layout);
}

export async function getDashboard(dashboardId: string, signal?: AbortSignal): Promise<DashboardSpecification> {
  const response = await apiFetch<SaveDashboardResponse>(`/api/v1/dashboards/${dashboardId}`, { signal });
  return dashboardLayout(response.layout);
}
