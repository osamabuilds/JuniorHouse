import { Injectable, computed, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { lookupCacheKey } from '../utils/lookup-cache-key.util';
import { ReferenceLookupActions, selectLookupCache } from '../store';

const KEYS = ['sizes', 'colours', 'payment-terms', 'fabric-responsibilities'] as const;
type LookupKey = (typeof KEYS)[number];

/**
 * Turns the ids the API sends (a size, a colour, a payment term...) into the names staff know,
 * so no screen has to show "Size 4 / colour 1". Requests each list once, including retired values
 * (an old PO can still use one). Until a list arrives, or for an id it doesn't know, the name is
 * a neutral "#id" - never blank, never an error. Holds no state of its own: the lists live in the
 * reference-data store.
 */
@Injectable({ providedIn: 'root' })
export class LookupNames {
  private readonly store = inject(Store);
  private readonly cache = this.store.selectSignal(selectLookupCache);

  private readonly namesByKey = computed(() => {
    const cache = this.cache();
    const namesOf = (key: LookupKey): ReadonlyMap<number, string> =>
      new Map((cache[lookupCacheKey(key, true)] ?? []).map((item) => [item.id, item.name]));

    return {
      sizes: namesOf('sizes'),
      colours: namesOf('colours'),
      'payment-terms': namesOf('payment-terms'),
      'fabric-responsibilities': namesOf('fabric-responsibilities'),
    };
  });

  constructor() {
    for (const key of KEYS) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey: key, includeInactive: true }));
    }
  }

  sizeName(id: number): string {
    return this.name('sizes', id);
  }

  colourName(id: number): string {
    return this.name('colours', id);
  }

  paymentTermName(id: number | null): string {
    return id === null ? '—' : this.name('payment-terms', id);
  }

  fabricName(id: number | null): string {
    return id === null ? '—' : this.name('fabric-responsibilities', id);
  }

  private name(key: LookupKey, id: number): string {
    return this.namesByKey()[key].get(id) ?? `#${id}`;
  }
}
