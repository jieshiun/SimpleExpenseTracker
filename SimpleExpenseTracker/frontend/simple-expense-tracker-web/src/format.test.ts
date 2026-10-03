import { describe, it, expect } from 'vitest';
import { money, localDate } from './format';
describe('format', () => {
  it('preserves decimal money', () => expect(money(1280.5)).toContain('1,280.5'));
  it('formats the local calendar day', () => expect(localDate(new Date(2026, 9, 3))).toBe('2026-10-03'));
});
