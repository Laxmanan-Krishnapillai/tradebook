import { describe, expect, it } from 'vitest';
import { zMoney } from '../../src/api/generated/zod.gen';
import { isMoneyString, moneyInputField, normalizeMoneyInput } from '../../src/lib/validation/money-input';

describe('normalizeMoneyInput', () => {
  it.each([
    ['31.5', '31.5'],
    [' 31.50 ', '31.50'],
    ['007', '7'],
    ['.5', '0.5'],
    ['5.', '5'],
    ['-0.25', '-0.25'],
    ['+2', '2'],
    ['1e3', '1000'],
    ['1.5e-2', '0.015'],
    ['-0', '0'],
    ['0', '0'],
  ])('canonicalizes %s to the wire format %s', (raw, expected) => {
    const normalized = normalizeMoneyInput(raw);
    expect(normalized).toBe(expected);
    expect(zMoney.safeParse(normalized).success).toBe(true);
  });

  it('preserves decimal digits a float round-trip would destroy', () => {
    expect(normalizeMoneyInput('12345678901234567.891')).toBe('12345678901234567.891');
  });

  it.each(['abc', '.', '1.2.3', '1e2.5', 'Infinity', 'NaN', '12,5'])(
    'leaves the non-numeric input %s for zMoney to reject',
    (raw) => expect(isMoneyString(normalizeMoneyInput(raw))).toBe(false),
  );
});

describe('moneyInputField', () => {
  const required = moneyInputField({ label: 'TTF EUR/MWh', required: true });

  it('outputs the canonical Money string for raw input text', () => {
    expect(required.parse('0031.50')).toBe('31.50');
  });

  it('rejects a missing required amount with a labelled message', () => {
    expect(() => required.parse(null)).toThrow('TTF EUR/MWh is required.');
    expect(() => required.parse('  ')).toThrow('TTF EUR/MWh is required.');
  });

  it('keeps optional empties as null or undefined', () => {
    const optional = moneyInputField({ label: 'EUR/USD' });
    expect(optional.parse(null)).toBeNull();
    expect(optional.parse(undefined)).toBeUndefined();
  });

  it('rejects non-decimal text with a labelled message', () => {
    expect(() => required.parse('12,5')).toThrow('TTF EUR/MWh must be a decimal number (for example 31.5).');
  });

  it('rejects a JSON number: Money travels as a string, never a number', () => {
    expect(() => required.parse(31.5)).toThrow('TTF EUR/MWh must be entered as a decimal amount.');
  });

  it('enforces the positive and nonnegative bounds', () => {
    expect(() => moneyInputField({ label: 'EUR/USD', positive: true }).parse('0')).toThrow('EUR/USD must be greater than zero.');
    expect(() => moneyInputField({ label: 'Volume', nonnegative: true }).parse('-1')).toThrow('Volume cannot be negative.');
    expect(moneyInputField({ label: 'Volume', nonnegative: true }).parse('0')).toBe('0');
  });
});
