export interface LookupDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
  readonly sortSeq: number;
  readonly isActive: boolean;
  readonly parentCategoryId: number | null;
  readonly defaultAdvancePercent: number | null;
  /** The ISO 3758 symbol key of a care instruction (Sprint 7); absent for every other lookup. */
  readonly symbolCode?: string | null;
}

export interface LookupFormValue {
  readonly code: string;
  readonly name: string;
  readonly description: string | null;
  readonly sortSeq: number;
  readonly parentCategoryId?: number | null;
  readonly defaultAdvancePercent?: number | null;
}
