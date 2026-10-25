import { SizeChartRow } from '../../models/product-page.model';
import { ageRange, columnsFor, range } from './size-chart.format';

const row = (over: Partial<SizeChartRow>): SizeChartRow => ({
  sizeCode: '5-6y', sizeName: '5-6 Years', ageMinMonths: null, ageMaxMonths: null, heightMinCm: null, heightMaxCm: null,
  weightMinKg: null, weightMaxKg: null, chestCm: null, waistCm: null, lengthCm: null, selected: false, ...over,
});

describe('size chart format', () => {
  it('PDP-6 writes a range, or one end of it', () => {
    expect(range(110, 122)).toBe('110 to 122');
    expect(range(110, 110)).toBe('110');
    expect(range(110, null)).toBe('from 110');
    expect(range(null, 122)).toBe('up to 122');
    expect(range(null, null)).toBeNull();
  });

  it('PDP-6 writes ages in months under two years and in whole years from then on', () => {
    expect(ageRange(6, 12)).toBe('6 to 12 months');
    expect(ageRange(1, 1)).toBe('1 month');
    expect(ageRange(24, 36)).toBe('2 to 3 years');
    expect(ageRange(60, 83)).toBe('5 to 6 years');
    expect(ageRange(60, 71)).toBe('5 years');
    expect(ageRange(12, 36)).toBe('12 to 36 months');
    expect(ageRange(36, null)).toBe('from 3 years');
    expect(ageRange(null, 18)).toBe('up to 18 months');
    expect(ageRange(null, null)).toBeNull();
  });

  it('PDP-6 shows only the columns some row has, in a fixed order', () => {
    const rows = [row({ chestCm: 60, ageMinMonths: 60, ageMaxMonths: 83 }), row({ chestCm: 64 })];

    expect(columnsFor(rows).map((c) => c.key)).toEqual(['age', 'chest']);
    expect(columnsFor([row({})])).toEqual([]);
  });
});
