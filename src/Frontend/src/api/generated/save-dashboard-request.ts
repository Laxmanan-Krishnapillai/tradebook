/**
 * This is a TypeGen auto-generated file.
 * Any changes made to this file can be lost when this file is regenerated.
 */

import { DashboardSpecification } from "../../types/visualizations";

export interface SaveDashboardRequest {
    dashboardId: string;
    version: number;
    layout: DashboardSpecification;
}
