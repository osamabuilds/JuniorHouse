import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ListingFilter, toCanonicalQuery } from '../../models/listing-query';

export interface FilterChip {
  readonly label: string;
  /** The filter with just this one removed. */
  readonly without: ListingFilter;
}

/** One chip per active filter, each a link to the same listing without it, and a link that clears them all (LST-3, LST-6). */
@Component({
  selector: 'rp-active-filters',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './active-filters.component.html',
  styleUrl: './active-filters.component.scss',
})
export class ActiveFiltersComponent {
  readonly chips = input.required<readonly FilterChip[]>();
  readonly clearAll = input.required<ListingFilter>();
  readonly path = input.required<string>();

  protected query(filter: ListingFilter): Record<string, string | string[]> {
    return toCanonicalQuery(filter);
  }
}
