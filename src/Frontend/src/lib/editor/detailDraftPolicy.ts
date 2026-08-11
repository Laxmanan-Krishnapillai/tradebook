export function shouldAdoptRefreshedDraft({
  activeVersion,
  refreshedVersion,
  dirty,
  refreshedMatchesDraft,
}: {
  activeVersion: number;
  refreshedVersion: number;
  dirty: boolean;
  refreshedMatchesDraft: boolean;
}): boolean {
  if (refreshedVersion <= activeVersion) return false;
  return !dirty || refreshedMatchesDraft;
}

function canonicalDecimal(value: string): string | undefined {
  const match = value.trim().match(/^([+-]?)(\d+)(?:\.(\d*))?$/);
  if (!match) return undefined;
  const sign = match[1] === '-' ? '-' : '';
  const integer = (match[2] ?? '').replace(/^0+(?=\d)/, '') || '0';
  const fraction = (match[3] ?? '').replace(/0+$/, '');
  const unsigned = fraction ? `${integer}.${fraction}` : integer;
  return unsigned === '0' ? '0' : `${sign}${unsigned}`;
}

export function draftValuesEquivalent(left: unknown, right: unknown): boolean {
  if (Object.is(left, right)) return true;
  if (typeof left !== 'string' || typeof right !== 'string') return false;
  const leftDecimal = canonicalDecimal(left);
  const rightDecimal = canonicalDecimal(right);
  if (leftDecimal !== undefined && rightDecimal !== undefined) return leftDecimal === rightDecimal;
  return left.trim() === right.trim();
}

export function changedFields<T extends object>(before: T, after: T): (keyof T)[] {
  return (Object.keys(after) as (keyof T)[]).filter((key) => (
    !draftValuesEquivalent(before[key], after[key])
  ));
}
