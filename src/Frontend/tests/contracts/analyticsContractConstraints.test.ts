import { describe, expect, it } from 'vitest';
import { zAnalyticsQueryBody } from '../../src/api/generated/zod.gen';

const identifier = 'member';
const filter = { member: identifier, operator: 'equals', values: ['value'] };
const sort = { member: identifier, direction: 'asc' };
const timeDimension = { dimension: identifier, granularity: 'month' };

describe('generated analytics request constraints', () => {
  it('accepts every representable collection boundary', () => {
    const boundaryQueries = [
      { modelName: identifier, measures: Array<string>(64).fill(identifier) },
      { modelName: identifier, metrics: Array<string>(64).fill(identifier) },
      { modelName: identifier, dimensions: Array<string>(64).fill(identifier) },
      { modelName: identifier, timeDimensions: Array(16).fill(timeDimension) },
      { modelName: identifier, filters: Array(64).fill(filter) },
      { modelName: identifier, sorts: Array(16).fill(sort) },
      {
        modelName: identifier,
        filters: [{ ...filter, values: Array<number>(256).fill(1) }],
      },
    ];

    for (const query of boundaryQueries) {
      expect(zAnalyticsQueryBody.safeParse(query).success).toBe(true);
    }
  });

  it('rejects every collection immediately above its limit', () => {
    const oversizedQueries = [
      { modelName: identifier, measures: Array<string>(65).fill(identifier) },
      { modelName: identifier, metrics: Array<string>(65).fill(identifier) },
      { modelName: identifier, dimensions: Array<string>(65).fill(identifier) },
      { modelName: identifier, timeDimensions: Array(17).fill(timeDimension) },
      { modelName: identifier, filters: Array(65).fill(filter) },
      { modelName: identifier, sorts: Array(17).fill(sort) },
      {
        modelName: identifier,
        filters: [{ ...filter, values: Array<number>(257).fill(1) }],
      },
    ];

    for (const query of oversizedQueries) {
      expect(zAnalyticsQueryBody.safeParse(query).success).toBe(false);
    }
  });

  it('enforces identifier limits across the complete query shape', () => {
    const oversized = 'x'.repeat(129);
    const oversizedQueries = [
      { modelName: oversized },
      { modelName: identifier, measures: [oversized] },
      { modelName: identifier, metrics: [oversized] },
      { modelName: identifier, dimensions: [oversized] },
      {
        modelName: identifier,
        timeDimensions: [{ ...timeDimension, dimension: oversized }],
      },
      {
        modelName: identifier,
        timeDimensions: [{ ...timeDimension, granularity: oversized }],
      },
      { modelName: identifier, filters: [{ ...filter, member: oversized }] },
      { modelName: identifier, sorts: [{ ...sort, member: oversized }] },
      { modelName: identifier, sorts: [{ ...sort, direction: oversized }] },
    ];

    for (const query of oversizedQueries) {
      expect(zAnalyticsQueryBody.safeParse(query).success).toBe(false);
    }
  });

  it('constrains string filter values while preserving number and boolean values', () => {
    const maximum = 'x'.repeat(1024);
    expect(
      zAnalyticsQueryBody.safeParse({
        modelName: identifier,
        filters: [{ ...filter, values: [maximum, 42, true] }],
        timeDimensions: [{ ...timeDimension, dateRange: [maximum] }],
      }).success,
    ).toBe(true);

    const oversized = `${maximum}x`;
    expect(
      zAnalyticsQueryBody.safeParse({
        modelName: identifier,
        filters: [{ ...filter, values: [oversized] }],
      }).success,
    ).toBe(false);
    expect(
      zAnalyticsQueryBody.safeParse({
        modelName: identifier,
        timeDimensions: [{ ...timeDimension, dateRange: [oversized] }],
      }).success,
    ).toBe(false);
  });

  it('preserves the full signed int32 offset contract', () => {
    expect(zAnalyticsQueryBody.safeParse({ modelName: identifier, offset: 2_147_483_647 }).success).toBe(true);
    expect(zAnalyticsQueryBody.safeParse({ modelName: identifier, offset: -2_147_483_648 }).success).toBe(true);
    expect(zAnalyticsQueryBody.safeParse({ modelName: identifier, offset: 2_147_483_648 }).success).toBe(false);
  });
});
