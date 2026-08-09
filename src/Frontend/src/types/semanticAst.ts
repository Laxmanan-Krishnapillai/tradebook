type FilterOperator = 'equals' | 'notEquals' | 'contains' | 'greaterThan' | 'greaterThanOrEqual' | 'lessThan' | 'lessThanOrEqual' | 'in' | 'notIn';
type TimeGranularity = 'day' | 'week' | 'month' | 'quarter' | 'year';
interface TimeDimensionQuery { dimension: string; granularity: TimeGranularity; dateRange?: [string, string]; }
interface FilterQuery { member: string; operator: FilterOperator; values: (string | number | boolean)[]; }
interface SortQuery { member: string; direction: 'asc' | 'desc'; }
export interface JsonQueryAst { modelName: string; measures?: string[]; metrics?: string[]; dimensions?: string[]; timeDimensions?: TimeDimensionQuery[]; filters?: FilterQuery[]; sorts?: SortQuery[]; limit?: number; offset?: number; }
