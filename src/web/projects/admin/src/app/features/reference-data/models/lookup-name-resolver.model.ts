/**
 * The id -> name lookups presentational components need. Containers pass the `LookupNames` facade
 * in; components depend on this shape only, so they inject nothing.
 */
export interface LookupNameResolver {
  sizeName(id: number): string;
  colourName(id: number): string;
  paymentTermName(id: number | null): string;
  fabricName(id: number | null): string;
}
