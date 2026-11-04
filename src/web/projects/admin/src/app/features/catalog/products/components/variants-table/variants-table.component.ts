import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { inputValue } from '@shared';
import { VariantDto, VariantPriceInput } from '../../models';
import { HasPermissionDirective } from '@core/auth';

/** What the person has typed for one variant, as text, until it is saved. */
interface Draft {
  price: string;
  compareAt: string;
}

const key = (variant: VariantDto) => `${variant.colourId}:${variant.sizeId}`;

/**
 * The Variants tab (PRD-1, PRD-2, PRD-13): every planned size × colour with its price and compare-at ("was") price. A variant shows its
 * SKU and barcode once Labelling has issued them and reads "Not yet available" until then; it can be priced before that. Prices are PKR.
 * A compare-at price must be higher than the price (D4), and the form says so before anything is sent.
 */
@Component({
  imports: [HasPermissionDirective],
  selector: 'app-variants-table',
  template: `
    <p class="field-hint">
      Prices are in PKR. A variant can be bought as soon as Labelling has issued its barcode and it has a price; until then it reads "Not yet
      available". The compare-at price is the optional "was" price and must be higher than the price.
    </p>

    @if (problems().length > 0) {
      <p class="banner banner--error" role="alert">{{ problems()[0] }}</p>
    }

    <table class="responsive">
      <caption class="sr-only">Variants and prices</caption>
      <thead>
        <tr>
          <th scope="col">Colour</th>
          <th scope="col">Size</th>
          <th scope="col">SKU and barcode</th>
          <th scope="col">Price (PKR)</th>
          <th scope="col">Compare-at price (PKR)</th>
          <th scope="col">Status</th>
        </tr>
      </thead>
      <tbody>
        @for (variant of variants(); track variant.id) {
          <tr>
            <th scope="row" data-label="Colour">{{ variant.colourName }}</th>
            <td data-label="Size">{{ variant.sizeName }}</td>
            <td data-label="SKU and barcode">
              @if (variant.skuCode) {
                {{ variant.skuCode }}<br /><small>{{ variant.gtin }}</small>
              } @else {
                <span class="badge badge--muted">Not yet available</span>
              }
            </td>
            <td data-label="Price (PKR)">
              <label>
                <span class="sr-only">Price for {{ variant.colourName }} {{ variant.sizeName }}</span>
                <input type="text" inputmode="decimal" [value]="draftOf(variant).price" [disabled]="disabled()" (input)="setPrice(variant, $event)" />
              </label>
            </td>
            <td data-label="Compare-at price (PKR)">
              <label>
                <span class="sr-only">Compare-at price for {{ variant.colourName }} {{ variant.sizeName }}</span>
                <input type="text" inputmode="decimal" [value]="draftOf(variant).compareAt" [disabled]="disabled()" (input)="setCompare(variant, $event)" />
              </label>
            </td>
            <td data-label="Status">
              @switch (variant.availability) {
                @case ('Sellable') {
                  <span class="badge">Can be bought</span>
                }
                @case ('Unpriced') {
                  <span class="badge badge--muted">Needs a price</span>
                }
                @case ('GtinVoided') {
                  <span class="badge badge--muted">Barcode voided</span>
                }
                @default {
                  <span class="badge badge--muted">Waiting for a barcode</span>
                }
              }
            </td>
          </tr>
        }
      </tbody>
    </table>

    <div class="form-actions">
      <button type="button" class="button button--primary" [disabled]="disabled() || saving()" (click)="save()">
        {{ saving() ? 'Saving…' : 'Save prices' }}
      </button>
      <button *hasPermission="'catalog.write'" type="button" class="button" [disabled]="saving()" (click)="refreshRequested.emit()">Read barcodes again</button>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VariantsTableComponent {
  readonly variants = input.required<readonly VariantDto[]>();
  readonly saving = input(false);
  readonly disabled = input(false);

  readonly pricesSaved = output<readonly VariantPriceInput[]>();
  readonly refreshRequested = output<void>();

  private readonly drafts = signal<ReadonlyMap<string, Draft>>(new Map());
  readonly problems = signal<readonly string[]>([]);

  constructor() {
    // A fresh product from the server replaces whatever was typed.
    effect(() => {
      const variants = this.variants();
      untracked(() => {
        this.drafts.set(new Map(variants.map((v) => [key(v), { price: v.price === null ? '' : String(v.price), compareAt: v.compareAtPrice === null ? '' : String(v.compareAtPrice) }])));
        this.problems.set([]);
      });
    });
  }

  readonly hasDrafts = computed(() => this.drafts().size > 0);

  draftOf(variant: VariantDto): Draft {
    return this.drafts().get(key(variant)) ?? { price: '', compareAt: '' };
  }

  setPrice(variant: VariantDto, event: Event): void {
    this.update(variant, { price: inputValue(event) });
  }

  setCompare(variant: VariantDto, event: Event): void {
    this.update(variant, { compareAt: inputValue(event) });
  }

  save(): void {
    const prices: VariantPriceInput[] = [];
    const problems: string[] = [];

    for (const variant of this.variants()) {
      const draft = this.draftOf(variant);
      const label = `${variant.colourName} ${variant.sizeName}`;
      if (draft.price.trim() === '') {
        if (draft.compareAt.trim() !== '') {
          problems.push(`${label}: enter a price before a compare-at price.`);
        }
        continue;
      }

      const price = Number(draft.price);
      const compareAt = draft.compareAt.trim() === '' ? null : Number(draft.compareAt);
      if (!Number.isFinite(price) || price <= 0) {
        problems.push(`${label}: the price must be more than 0.`);
      } else if (compareAt !== null && (!Number.isFinite(compareAt) || compareAt <= price)) {
        problems.push(`${label}: the compare-at price must be higher than the price.`);
      } else if (price !== variant.price || compareAt !== variant.compareAtPrice) {
        prices.push({ colourId: variant.colourId, sizeId: variant.sizeId, price, compareAtPrice: compareAt });
      }
    }

    this.problems.set(problems);
    if (problems.length === 0 && prices.length > 0) {
      this.pricesSaved.emit(prices);
    }
  }

  private update(variant: VariantDto, change: Partial<Draft>): void {
    const next = new Map(this.drafts());
    next.set(key(variant), { ...this.draftOf(variant), ...change });
    this.drafts.set(next);
  }
}
