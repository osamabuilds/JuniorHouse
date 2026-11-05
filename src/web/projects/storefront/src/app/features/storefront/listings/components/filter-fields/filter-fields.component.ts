import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ListingOptions } from '../../models/listing.model';
import { ListingFilter } from '../../models/listing-query';

export type FilterGroup = 'age' | 'size' | 'colour' | 'price' | 'sort';

/**
 * The form controls of the filter bar, as plain inputs inside whatever form holds them (FLT-4, FLT-6): the same names the listing already
 * reads (`size`, `colour`, `age`, `priceMin`, `priceMax`, `sort`), so a GET submit works with no scripting. One group, or all of them in the
 * phone sheet. A colour swatch is a native checkbox drawn as a round mark with its initial (the colour lookup carries no colour value, so
 * nothing is guessed) and its name in words, so colour is never the only signal. There are no ids: each control sits inside its own label.
 */
@Component({
  selector: 'rp-filter-fields',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filter-fields.component.html',
})
export class FilterFieldsComponent {
  readonly options = input.required<ListingOptions>();
  readonly filter = input.required<ListingFilter>();
  /** Only this group, or every group when null. */
  readonly group = input<FilterGroup | null>(null);
  readonly defaultSortLabel = input('Newest');

  protected show(group: FilterGroup): boolean {
    const g = this.group();
    return g === null || g === group;
  }

  protected has(list: readonly string[], value: string): boolean {
    return list.includes(value);
  }

  protected initial(label: string): string {
    return label.trim().charAt(0).toUpperCase();
  }
}
