import type { SaveDashboardRequest } from '../../api/generated/save-dashboard-request';
import type { SaveDashboardResponse } from '../../api/generated/save-dashboard-response';
import type { DashboardSpecification } from '../../types/visualizations';
import { apiFetch } from './client';

export async function saveDashboard(dashboard: DashboardSpecification): Promise<DashboardSpecification> {
  const request: SaveDashboardRequest = { dashboardId: dashboard.dashboardId, version: dashboard.version, layout: dashboard as unknown as SaveDashboardRequest['layout'] };
  const response = await apiFetch<SaveDashboardResponse>(`/api/v1/dashboards/${dashboard.dashboardId}`, { method: 'PUT', body: JSON.stringify(request) });
  return response.layout as unknown as DashboardSpecification;
}

export async function getDashboard(dashboardId: string): Promise<DashboardSpecification> {
  const response = await apiFetch<SaveDashboardResponse>(`/api/v1/dashboards/${dashboardId}`);
  return response.layout as unknown as DashboardSpecification;
}
