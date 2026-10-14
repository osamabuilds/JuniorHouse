/** One row of a size chart (docs/specs/catalog/size-charts/spec.md): ages in months, lengths in cm, weight in kg. */
export interface SizeChartLineDto {
  readonly sizeId: number;
  readonly sizeName: string;
  readonly ageMinMonths: number | null;
  readonly ageMaxMonths: number | null;
  readonly heightMinCm: number | null;
  readonly heightMaxCm: number | null;
  readonly weightMinKg: number | null;
  readonly weightMaxKg: number | null;
  readonly chestCm: number | null;
  readonly waistCm: number | null;
  readonly lengthCm: number | null;
}

export type SizeChartLineInput = Omit<SizeChartLineDto, 'sizeName'>;

export interface SizeChartDto {
  readonly id: number;
  readonly categoryId: number;
  readonly genderId: number;
  readonly name: string;
  readonly unit: string;
  readonly lines: readonly SizeChartLineDto[];
  readonly version: number;
}

export interface SizeChartRowDto {
  readonly id: number;
  readonly categoryId: number;
  readonly genderId: number;
  readonly name: string;
  readonly lineCount: number;
}

export interface SizeChartFilters {
  readonly page: number;
  readonly pageSize: number;
}

/** What the editor sends: a new chart carries its category and gender; an edit keeps the chart's own and sends the version. */
export interface SizeChartFormValue {
  readonly categoryId: number;
  readonly genderId: number;
  readonly name: string;
  readonly lines: readonly SizeChartLineInput[];
}

/** The columns of the editor, in order, as `[field, label, step]`. */
export const CHART_COLUMNS = [
  ['ageMinMonths', 'Age from (months)'],
  ['ageMaxMonths', 'Age to (months)'],
  ['heightMinCm', 'Height from (cm)'],
  ['heightMaxCm', 'Height to (cm)'],
  ['weightMinKg', 'Weight from (kg)'],
  ['weightMaxKg', 'Weight to (kg)'],
  ['chestCm', 'Chest (cm)'],
  ['waistCm', 'Waist (cm)'],
  ['lengthCm', 'Length (cm)'],
] as const;

export type ChartColumn = (typeof CHART_COLUMNS)[number][0];
