/** One bin as the API lists it (docs/specs/warehouse/bins/spec.md). */
export interface BinRowDto {
  readonly id: number;
  readonly binCode: string;
  readonly zone: string;
  readonly aisle: string;
  readonly shelf: string;
  readonly isActive: boolean;
  readonly unitsOnHand: number;
  readonly lastCountedAt: string | null;
}

/** The values the add-bin form sends. The server builds the code from the three parts (BIN-1). */
export interface CreateBinValue {
  readonly zone: string;
  readonly aisle: string;
  readonly shelf: string;
  readonly actingPerson: string;
}

export interface BinFilters {
  readonly zone: string;
  readonly page: number;
  readonly pageSize: number;
}
