import { describe, expect, it } from 'vitest';
import { changedFields, draftValuesEquivalent, shouldAdoptRefreshedDraft } from '../../src/lib/editor/detailDraftPolicy';

describe('detail draft refresh policy', () => {
  it('adopts newer server state only when the draft is clean or matches that state', () => {
    expect(shouldAdoptRefreshedDraft({
      activeVersion: 4,
      refreshedVersion: 5,
      dirty: false,
      refreshedMatchesDraft: false,
    })).toBe(true);
    expect(shouldAdoptRefreshedDraft({
      activeVersion: 4,
      refreshedVersion: 5,
      dirty: true,
      refreshedMatchesDraft: false,
    })).toBe(false);
    expect(shouldAdoptRefreshedDraft({
      activeVersion: 4,
      refreshedVersion: 5,
      dirty: true,
      refreshedMatchesDraft: true,
    })).toBe(true);
  });

  it('ignores duplicate and stale refreshes', () => {
    expect(shouldAdoptRefreshedDraft({
      activeVersion: 5,
      refreshedVersion: 5,
      dirty: false,
      refreshedMatchesDraft: true,
    })).toBe(false);
  });

  it('treats server-normalized decimals and surrounding whitespace as the accepted draft', () => {
    expect(draftValuesEquivalent('12.5', '12.500000')).toBe(true);
    expect(draftValuesEquivalent('  Nordic contract  ', 'Nordic contract')).toBe(true);
    expect(changedFields(
      { volume: '12.500000', name: 'Nordic contract' },
      { volume: '12.5', name: ' Nordic contract ' },
    )).toEqual([]);
  });
});
