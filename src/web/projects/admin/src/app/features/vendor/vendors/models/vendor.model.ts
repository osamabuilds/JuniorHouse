export interface VendorDto {
  readonly id: number;
  readonly name: string;
  readonly contactName: string;
  readonly contactPhone: string;
  readonly contactEmail: string | null;
  readonly cityId: number;
  readonly paymentTermId: number;
  readonly onTimePercent: number | null;
  readonly onQuantityPercent: number | null;
  readonly defectRatePercent: number | null;
  readonly isActive: boolean;
  readonly specialisationIds: readonly number[];
}

export interface VendorSummaryDto {
  readonly id: number;
  readonly name: string;
  readonly cityId: number;
  readonly isActive: boolean;
}

export interface CreateVendorValue {
  readonly name: string;
  readonly contactName: string;
  readonly contactPhone: string;
  readonly contactEmail: string | null;
  readonly cityId: number;
  readonly paymentTermId: number;
  readonly specialisationIds: readonly number[];
}

export interface UpdateVendorValue extends Omit<CreateVendorValue, 'name'> {
  readonly isActive: boolean;
}

export interface VendorFilters {
  readonly page: number;
  readonly pageSize: number;
  readonly searchText: string;
  readonly specialisationId: number | null;
  readonly activeOnly: boolean;
}

/** The vendor scorecard (FR-SC-01, Sprint 4). Every measure is null when there is no data behind it, never 0% or 100%. */
export interface VendorScorecardDto {
  readonly vendorId: number;
  /** Length of the rolling window behind the `*Rolling` measures. */
  readonly windowDays: number;
  readonly deliveriesCounted: number;
  readonly deliveriesOnTime: number;
  readonly deliveriesOnQuantity: number;
  readonly deliveriesExcludedRompDelay: number;
  readonly inspectedUnits: number;
  readonly defectiveUnits: number;
  readonly onTimePct: number | null;
  readonly onQuantityPct: number | null;
  readonly defectRatePct: number | null;
  readonly onTimePctRolling: number | null;
  readonly onQuantityPctRolling: number | null;
  readonly defectRatePctRolling: number | null;
}
