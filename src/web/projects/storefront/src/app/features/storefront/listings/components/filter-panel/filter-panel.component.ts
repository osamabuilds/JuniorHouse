import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ListingOptions } from '../../models/listing.model';
import { ListingFilter, parseFilter } from '../../models/listing-query';

/**
 * The filters and the sort as a real GET form (LST-3, LST-12): without scripting the Apply button submits to the listing's own address;
 * with scripting the same fields are parsed here and handed to the page, which navigates without a reload.
 */
@Component({
  selector: 'rp-filter-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filter-panel.component.html',
  styleUrl: './filter-panel.component.scss',
})
export class FilterPanelComponent {
  readonly options = input.required<ListingOptions>();
  readonly filter = input.required<ListingFilter>();
  /** The path of the listing: the form action. */
  readonly action = input.required<string>();
  readonly open = input(false);
  /** Query values the form sends along with the filter (for example the search text), as hidden fields. */
  readonly extra = input<Readonly<Record<string, string>>>({});
  /** What the default order is called: "Newest" on a listing, "Best match" on search. */
  readonly defaultSortLabel = input('Newest');
  readonly applied = output<ListingFilter>();

  protected extraFields(): readonly [string, string][] {
    return Object.entries(this.extra());
  }

  protected has(list: readonly string[], value: string): boolean {
    return list.includes(value);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    const raw: Record<string, string[]> = {};
    for (const [k, v] of new FormData(event.target as HTMLFormElement).entries()) if (typeof v === 'string' && v !== '') (raw[k] ??= []).push(v);
    if (raw['priceMin'] || raw['priceMax']) raw['price'] = [`${raw['priceMin']?.[0] ?? ''}-${raw['priceMax']?.[0] ?? ''}`];
    this.applied.emit({ ...parseFilter(raw), page: 1 });
  }
}
