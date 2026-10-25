import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ListingFilter, toCanonicalQuery } from '../../models/listing-query';

/** Previous, a window of page numbers and next, as real links that keep the filter (LST-4). Hidden when there is one page. */
@Component({
  selector: 'rp-pagination',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.scss',
})
export class PaginationComponent {
  readonly page = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly path = input.required<string>();
  readonly filter = input.required<ListingFilter>();
  /** Query values every link keeps besides the filter (for example the search text). */
  readonly extra = input<Readonly<Record<string, string>>>({});

  protected readonly numbers = computed(() => {
    const from = Math.max(1, this.page() - 2);
    const to = Math.min(this.totalPages(), from + 4);
    return Array.from({ length: to - from + 1 }, (_, i) => from + i);
  });

  protected query(page: number): Record<string, string | string[]> {
    return { ...this.extra(), ...toCanonicalQuery({ ...this.filter(), page }, true) };
  }
}
