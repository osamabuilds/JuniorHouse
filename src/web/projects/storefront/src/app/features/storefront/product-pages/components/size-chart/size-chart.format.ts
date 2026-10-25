import { SizeChartRow } from '../../models/product-page.model';

/** A range as text: "110 to 122", one end alone as "from 110" or "up to 122", nothing as null. Pure. */
export function range(min: number | null, max: number | null): string | null {
  if (min !== null && max !== null) return min === max ? `${min}` : `${min} to ${max}`;
  if (min !== null) return `from ${min}`;
  if (max !== null) return `up to ${max}`;
  return null;
}

const months = (n: number): string => (n === 1 ? '1 month' : `${n} months`);
const years = (n: number): string => (n === 1 ? '1 year' : `${n} years`);

/**
 * An age range given in months, as words. From two years on both ends are whole years (60 to 83 months is "5 to 6 years", because 83
 * months is 6 years and 11 months); below that it stays in months. Pure.
 */
export function ageRange(min: number | null, max: number | null): string | null {
  const inYears = (min ?? max ?? 0) >= 24 && (max ?? min ?? 0) >= 24;
  if (min !== null && max !== null) {
    if (inYears) {
      const lo = Math.floor(min / 12);
      const hi = Math.floor(max / 12);
      return lo === hi ? years(lo) : `${lo} to ${hi} years`;
    }
    return min === max ? months(min) : `${min} to ${max} months`;
  }
  const one = min ?? max;
  if (one === null) return null;
  const text = one >= 24 ? years(Math.floor(one / 12)) : months(one);
  return min !== null ? `from ${text}` : `up to ${text}`;
}

export interface ChartColumn {
  readonly key: string;
  readonly heading: string;
  readonly value: (row: SizeChartRow) => string | null;
}

const ALL: readonly ChartColumn[] = [
  { key: 'age', heading: 'Age', value: (r) => ageRange(r.ageMinMonths, r.ageMaxMonths) },
  { key: 'height', heading: 'Height (cm)', value: (r) => range(r.heightMinCm, r.heightMaxCm) },
  { key: 'weight', heading: 'Weight (kg)', value: (r) => range(r.weightMinKg, r.weightMaxKg) },
  { key: 'chest', heading: 'Chest (cm)', value: (r) => (r.chestCm === null ? null : `${r.chestCm}`) },
  { key: 'waist', heading: 'Waist (cm)', value: (r) => (r.waistCm === null ? null : `${r.waistCm}`) },
  { key: 'length', heading: 'Length (cm)', value: (r) => (r.lengthCm === null ? null : `${r.lengthCm}`) },
];

/** Only the columns that have a value in some row, in a fixed order, so an empty column never shows (PDP-6). Centimetres only (Q-PDP-4). */
export function columnsFor(rows: readonly SizeChartRow[]): readonly ChartColumn[] {
  return ALL.filter((c) => rows.some((r) => c.value(r) !== null));
}
