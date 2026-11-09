import { ChangeDetectionStrategy, Component, ElementRef, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { Observable, Subject, catchError, debounceTime, filter as keep, of, switchMap } from 'rxjs';
import { RpSheet } from '../../directives/sheet.directive';
import { ListingOptions } from '../../models/listing.model';
import { ListingFilter, toCanonicalQuery } from '../../models/listing-query';
import { filterFromForm } from '../../utils/filter-form.util';
import { FilterFieldsComponent, FilterGroup } from '../filter-fields/filter-fields.component';

interface GroupView {
  readonly key: FilterGroup;
  readonly label: string;
  readonly selected: number;
  /** The query of the same listing with just this group cleared (the chip's remove link). */
  readonly removeQuery: Record<string, string | string[]>;
}

const COUNT_PAUSE_MS = 250;

/**
 * The listing's filter bar (design system: FilterBar; FLT-1..13). Two server-rendered forms, one for each screen size, each a plain GET form so
 * the filters work with no scripting: on a desktop a chip per group opens a small popover; on a phone one "Filter and sort" disclosure becomes
 * a bottom sheet with every group and a live "Show N pieces" button. The page does the filtering; this only reads the form and says what the
 * visitor chose.
 */
@Component({
  selector: 'rp-filter-bar',
  imports: [RouterLink, FilterFieldsComponent, RpSheet],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './filter-bar.component.html',
  host: { '(document:click)': 'onDocumentClick($event)', '(keydown.escape)': 'onEscape($event)' },
})
export class FilterBarComponent {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;

  readonly options = input.required<ListingOptions>();
  readonly filter = input.required<ListingFilter>();
  /** The path of the listing: the form action. */
  readonly action = input.required<string>();
  /** The number of pieces for the filter now applied. */
  readonly total = input.required<number>();
  /** Query values every link and form keeps besides the filter (for example the search text). */
  readonly extra = input<Readonly<Record<string, string>>>({});
  /** What the default order is called: "Newest" on a listing, "Best match" on search. */
  readonly defaultSortLabel = input('Newest');
  /** Asks the server how many pieces a not yet applied filter would show; null means not known. Used by the phone sheet's button. */
  readonly countFor = input<((filter: ListingFilter) => Observable<number | null>) | null>(null);
  readonly applied = output<ListingFilter>();

  private readonly draft$ = new Subject<ListingFilter>();
  private readonly draftCount = signal<number | null>(null);
  /** The number on the sheet's button: the server's count for what is ticked, or the applied count until that is known. */
  protected readonly shown = computed(() => this.draftCount() ?? this.total());

  protected readonly groups = computed((): readonly GroupView[] => {
    const o = this.options();
    const f = this.filter();
    const query = (cleared: ListingFilter) => ({ ...this.extra(), ...toCanonicalQuery({ ...cleared, page: 1 }) });
    const all: (GroupView & { present: boolean })[] = [
      { key: 'age', label: 'Age', present: o.ages.length > 0, selected: f.age ? 1 : 0, removeQuery: query({ ...f, age: null }) },
      { key: 'size', label: 'Size', present: o.sizes.length > 0, selected: f.sizes.length, removeQuery: query({ ...f, sizes: [] }) },
      { key: 'colour', label: 'Colour', present: o.colours.length > 0, selected: f.colours.length, removeQuery: query({ ...f, colours: [] }) },
      { key: 'price', label: 'Price', present: o.priceMin !== null, selected: f.priceMin !== null || f.priceMax !== null ? 1 : 0, removeQuery: query({ ...f, priceMin: null, priceMax: null }) },
      { key: 'stock', label: 'Availability', present: true, selected: f.inStockOnly ? 1 : 0, removeQuery: query({ ...f, inStockOnly: false }) },
    ];
    return all.filter((g) => g.present);
  });

  protected readonly activeCount = computed(() => this.groups().reduce((n, g) => n + g.selected, 0));

  constructor() {
    this.draft$
      .pipe(
        debounceTime(COUNT_PAUSE_MS),
        switchMap((draft) => this.countFor()?.(draft).pipe(catchError(() => of(null))) ?? of(null)),
        keep((n): n is number => n !== null),
        takeUntilDestroyed(),
      )
      .subscribe((n) => this.draftCount.set(n));
  }

  protected extraFields(): readonly [string, string][] {
    return Object.entries(this.extra());
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.applied.emit(filterFromForm(event.target as HTMLFormElement));
    this.closeAll();
  }

  /** The phone sheet: something was ticked or typed, so ask for the new count after a short pause. */
  protected draftChanged(event: Event): void {
    this.draft$.next(filterFromForm(event.currentTarget as HTMLFormElement));
  }

  /** The sheet's own Close button: the same as Escape or a click on the scrim. */
  protected closeSheet(event: Event): void {
    const sheet = (event.target as HTMLElement).closest('details');
    if (!sheet) return;
    sheet.open = false;
    sheet.querySelector<HTMLElement>('summary')?.focus();
  }

  protected onEscape(event: Event): void {
    const open = this.host.querySelector<HTMLDetailsElement>('details.rp-chip-pop[open]');
    if (!open) return;
    open.open = false;
    open.querySelector<HTMLElement>('summary')?.focus();
    event.stopPropagation();
  }

  protected onDocumentClick(event: Event): void {
    for (const d of this.host.querySelectorAll<HTMLDetailsElement>('details.rp-chip-pop[open]')) {
      if (!d.contains(event.target as Node)) d.open = false;
    }
  }

  private closeAll(): void {
    for (const d of this.host.querySelectorAll<HTMLDetailsElement>('details[open]')) d.open = false;
  }
}
