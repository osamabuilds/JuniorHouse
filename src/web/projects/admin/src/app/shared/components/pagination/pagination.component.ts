import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { selectNumberOrNull } from '../../utils/dom-events.util';

export const PAGE_SIZE_OPTIONS: readonly number[] = [10, 25, 50];

/** Previous / next controls, "Showing a-b of n" and a page-size picker for a list. Holds no state. */
@Component({
  selector: 'app-pagination',
  templateUrl: './pagination.component.html',
  styleUrl: './pagination.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PaginationComponent {
  readonly total = input.required<number>();
  readonly page = input.required<number>();
  readonly pageSize = input.required<number>();

  readonly pageChanged = output<number>();
  readonly pageSizeChanged = output<number>();

  readonly sizes = PAGE_SIZE_OPTIONS;
  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize())));
  readonly first = computed(() => (this.total() === 0 ? 0 : (this.page() - 1) * this.pageSize() + 1));
  readonly last = computed(() => Math.min(this.total(), this.page() * this.pageSize()));

  onSizeChange(event: Event): void {
    this.pageSizeChanged.emit(selectNumberOrNull(event) ?? PAGE_SIZE_OPTIONS[0]);
  }
}
