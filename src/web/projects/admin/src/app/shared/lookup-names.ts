import { Injectable, inject, signal } from '@angular/core';
import { ReferenceApiService } from '../reference-data/reference-api.service';

const KEYS = ['sizes', 'colours', 'payment-terms', 'fabric-responsibilities'] as const;
type LookupKey = (typeof KEYS)[number];

/**
 * Turns the ids the API sends (a size, a colour, a payment term...) into the names staff know,
 * so no screen has to show "Size 4 / colour 1". Loads each list once, including retired values
 * (an old PO can still use one). Until a list arrives, or for an id it doesn't know, the name is
 * a neutral "#id" - never blank, never an error.
 */
@Injectable({ providedIn: 'root' })
export class LookupNames {
  private readonly reference = inject(ReferenceApiService);
  private readonly names = signal<Readonly<Record<LookupKey, ReadonlyMap<number, string>>>>({
    sizes: new Map(),
    colours: new Map(),
    'payment-terms': new Map(),
    'fabric-responsibilities': new Map(),
  });

  constructor() {
    for (const key of KEYS) {
      this.reference.list(key, true).subscribe({
        next: (items) =>
          this.names.update((current) => ({ ...current, [key]: new Map(items.map((item) => [item.id, item.name])) })),
        error: () => undefined, // the fallback "#id" keeps the screen usable
      });
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
    return this.names()[key].get(id) ?? `#${id}`;
  }
}
