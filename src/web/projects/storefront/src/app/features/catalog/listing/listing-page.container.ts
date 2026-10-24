import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AGE_BANDS, CATEGORIES, productsFor } from '../../../shared/data/mock-catalog';
import { ProductCardComponent } from '../../../shared/product-card/product-card.component';
import { IconComponent } from '../../../shared/ui/icon/icon.component';

const PAGE_SIZE = 8;
const SORTS = [
  { value: 'featured', label: 'Featured' },
  { value: 'price-asc', label: 'Price: low to high' },
  { value: 'price-desc', label: 'Price: high to low' },
] as const;

/** Listing (design system ListingPage). Age and sort live in the query string so a filtered page can be shared. */
@Component({
  selector: 'app-listing-page',
  imports: [RouterLink, IconComponent, ProductCardComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './listing-page.container.html',
})
export class ListingPageContainer {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly params = toSignal(this.route.paramMap, { requireSync: true });
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly ageBands = AGE_BANDS;
  protected readonly sorts = SORTS;
  protected readonly shown = signal(PAGE_SIZE);

  protected readonly category = computed(() => CATEGORIES.find((c) => c.slug === this.params().get('category')) ?? CATEGORIES[0]);
  protected readonly age = computed(() => AGE_BANDS.find((b) => b.value === this.query().get('age')) ?? null);
  protected readonly sort = computed(() => SORTS.find((s) => s.value === this.query().get('sort'))?.value ?? 'featured');

  protected readonly filtered = computed(() => {
    const band = this.age();
    const items = productsFor(this.category().slug).filter((p) => !band || (p.minAge <= band.max && p.maxAge >= band.min));
    const sort = this.sort();
    return sort === 'featured' ? items : [...items].sort((a, b) => (sort === 'price-asc' ? a.price - b.price : b.price - a.price));
  });
  protected readonly visible = computed(() => this.filtered().slice(0, this.shown()));
  protected readonly firstRow = computed(() => this.visible().slice(0, 4));
  protected readonly rest = computed(() => this.visible().slice(4));

  protected setAge(value: string | null): void {
    this.update({ age: value });
  }

  protected setSort(value: string): void {
    this.update({ sort: value === 'featured' ? null : value });
  }

  protected showMore(): void {
    this.shown.update((n) => n + PAGE_SIZE);
  }

  private update(queryParams: Record<string, string | null>): void {
    this.shown.set(PAGE_SIZE);
    void this.router.navigate([], { relativeTo: this.route, queryParams, queryParamsHandling: 'merge' });
  }
}
