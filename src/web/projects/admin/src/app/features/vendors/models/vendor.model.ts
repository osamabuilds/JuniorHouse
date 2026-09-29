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
