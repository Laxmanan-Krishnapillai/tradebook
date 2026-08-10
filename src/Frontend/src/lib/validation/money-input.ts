import { z } from 'zod';
import type { Money } from '../../api/generated/types.gen';
import { zMoney } from '../../api/generated/zod.gen';

// The generated wire contract declares Money as a decimal STRING (`zMoney`), never a JSON
// number. This module is the single place that turns raw <input> text into that format:
// keep form state as the user's string, normalize + validate here, send the string.
const numericInputPattern = /^([+-]?)(\d*)(?:\.(\d*))?(?:[eE]([+-]?\d+))?$/;

/** True when a string already satisfies the generated `Money` wire format. */
export function isMoneyString(value: string): boolean {
  return zMoney.safeParse(value).success;
}

/**
 * Canonicalizes an HTML number-input string ("007", ".5", "5.", "1e3") into the `Money`
 * wire format without a float round-trip, so decimal precision survives. Strings that are
 * not numeric at all are returned trimmed but otherwise as-is, so `isMoneyString` (and the
 * schemas built on it) reject them with a visible message instead of sending garbage.
 */
export function normalizeMoneyInput(raw: string): string {
  const trimmed = raw.trim();
  const match = numericInputPattern.exec(trimmed);
  if (!match) return trimmed;
  const integerRaw = match[2] ?? '';
  const fractionRaw = match[3] ?? '';
  const digits = integerRaw + fractionRaw;
  const exponent = Number(match[4] ?? '0');
  if (digits === '' || Math.abs(exponent) > 10_000) return trimmed;
  const point = integerRaw.length + exponent;
  const integerDigits = point <= 0 ? '0' : point >= digits.length ? digits + '0'.repeat(point - digits.length) : digits.slice(0, point);
  const fractionDigits = point <= 0 ? '0'.repeat(-point) + digits : point >= digits.length ? '' : digits.slice(point);
  const integer = integerDigits.replace(/^0+(?=\d)/, '');
  const sign = match[1] === '-' && !/^0*$/.test(digits) ? '-' : '';
  return `${sign}${integer}${fractionDigits === '' ? '' : `.${fractionDigits}`}`;
}

export interface MoneyInputFieldOptions {
  /** Human-readable field label embedded in every validation message. */
  label: string;
  required?: boolean;
  /** Reject values <= 0 (FX rates). */
  positive?: boolean;
  /** Reject values < 0 (volumes). */
  nonnegative?: boolean;
}

function invalid(ctx: z.RefinementCtx, message: string): typeof z.NEVER {
  ctx.addIssue({ code: 'custom', message });
  return z.NEVER;
}

/**
 * Zod field schema for a form input bound to a `Money` wire field. Accepts the raw input
 * string (or null/undefined for untouched optional fields), normalizes it, validates it
 * against the generated `zMoney` contract, and outputs the canonical decimal string that
 * must go on the wire.
 */
export function moneyInputField(options: MoneyInputFieldOptions): z.ZodType<Money | null | undefined> {
  const { label, required = false, positive = false, nonnegative = false } = options;
  // .optional() marks the field input-optional so z.object accepts absent keys (untouched
  // inputs); the transform still runs for undefined, so `required` keeps its message.
  return z.unknown().optional().transform((value, ctx): Money | null | undefined => {
    if (value === undefined || value === null || (typeof value === 'string' && value.trim() === '')) {
      if (required) return invalid(ctx, `${label} is required.`);
      return value === undefined ? undefined : null;
    }
    if (typeof value !== 'string') return invalid(ctx, `${label} must be entered as a decimal amount.`);
    const normalized = normalizeMoneyInput(value);
    if (!isMoneyString(normalized)) return invalid(ctx, `${label} must be a decimal number (for example 31.5).`);
    if (positive && Number(normalized) <= 0) return invalid(ctx, `${label} must be greater than zero.`);
    if (nonnegative && Number(normalized) < 0) return invalid(ctx, `${label} cannot be negative.`);
    return normalized;
  });
}
