export interface StyleTargetLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly targetQty: number;
}

export interface StyleDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly collectionName: string | null;
  readonly categoryId: number;
  readonly genderId: number;
  readonly ageBracketId: number;
  readonly fabricId: number;
  readonly targetUnitCost: number;
  readonly targetRetailPrice: number;
  readonly isActive: boolean;
  readonly colourIds: readonly number[];
  readonly sizeIds: readonly number[];
  readonly targetLines: readonly StyleTargetLineDto[];
}

export interface StyleSummaryDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly categoryId: number;
  readonly isActive: boolean;
}

export interface StyleFormValue {
  readonly code: string;
  readonly name: string;
  readonly collectionName: string | null;
  readonly categoryId: number;
  readonly genderId: number;
  readonly ageBracketId: number;
  readonly fabricId: number;
  readonly targetUnitCost: number;
  readonly targetRetailPrice: number;
  readonly colourIds: readonly number[];
  readonly sizeIds: readonly number[];
  readonly targetLines: readonly StyleTargetLineDto[];
}

export interface StyleFilters {
  readonly searchText: string;
  readonly categoryId: number | null;
  readonly activeOnly: boolean;
}
