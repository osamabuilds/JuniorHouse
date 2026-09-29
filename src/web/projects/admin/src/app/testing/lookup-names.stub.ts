import { LookupNameResolver } from '@features/reference-data';

/** A fixed id -> name resolver for presentational component tests (no store needed). */
export const LOOKUP_NAMES_STUB: LookupNameResolver = {
  sizeName: (id) => `Size ${id}`,
  colourName: (id) => `Colour ${id}`,
  paymentTermName: (id) => (id === null ? '—' : `Term ${id}`),
  fabricName: (id) => (id === null ? '—' : `Fabric ${id}`),
};
