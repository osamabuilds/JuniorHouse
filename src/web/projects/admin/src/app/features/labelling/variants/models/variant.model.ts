/** A SKU register row as the API returns it (docs/specs/labelling/variants/spec.md). */
export interface VariantRowDto {
  readonly id: number;
  readonly styleId: number;
  readonly styleCode: string;
  readonly styleName: string;
  readonly colourId: number;
  readonly colourName: string;
  readonly sizeId: number;
  readonly sizeName: string;
  readonly skuCode: string;
  /** The barcode number in use, or null when the variant's GTIN was voided and no new one has been assigned yet. */
  readonly activeGtin: string | null;
  readonly retiredCount: number;
  readonly createdAt: string;
}

/** One barcode number a variant has held. Retired numbers stay on record and are never issued again (NFR-SC-03). */
export interface GtinDto {
  readonly id: number;
  readonly gtin: string;
  readonly statusCode: 'Active' | 'Retired';
  readonly assignedAt: string;
  readonly retiredAt: string | null;
  readonly retiredReasonCode: string | null;
  readonly retiredNote: string | null;
  readonly retiredBy: string | null;
}

export interface VariantDetailDto {
  readonly variant: VariantRowDto;
  readonly gtins: readonly GtinDto[];
}

/** How much of the company's GTIN range is used (VAR-12). */
export interface GtinCapacityDto {
  readonly prefix: string;
  readonly isTestPrefix: boolean;
  readonly capacity: number;
  readonly used: number;
  readonly remaining: number;
  readonly usedPercent: number;
  readonly warning: boolean;
}

export type VariantStatusFilter = 'all' | 'active' | 'retired';

export interface VariantFilters {
  readonly searchText: string;
  readonly status: VariantStatusFilter;
  readonly page: number;
  readonly pageSize: number;
}

export interface VoidGtinValue {
  readonly reasonId: number;
  readonly note: string | null;
  readonly actingPerson: string;
}
