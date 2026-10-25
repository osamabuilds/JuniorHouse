import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { NotFoundComponent } from '../../../../../shared/ui/not-found/not-found.component';
import {
  ActiveFiltersComponent,
  EmptyStateComponent,
  FilterChip,
  FilterPanelComponent,
  ListingFilter,
  PaginationComponent,
  ProductTileComponent,
  parseFilter,
  toCanonicalQuery,
  toQueryString,
} from '../../../listings';
import { Crumb, BreadcrumbsComponent } from '../../../navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand, selectTopLinks } from '../../../site-settings';
import { SearchResult } from '../../models/search.model';
import { normaliseQuery, searchKey, searchUrl } from '../../models/search-query';
import { SearchResultsActions } from '../../store/search-results.actions';
import { selectSearchKey, selectSearchResult } from '../../store/search-results.selectors';

const PATH = '/search';

/**
 * The results of a search (FR-04, FR-05, FR-06): a listing for the typed words, or the product itself for an exact SKU. Server-rendered.
 * It is for visitors, not for Google: `noindex, follow`, canonical to the search without paging noise (SRC-11). An empty result is a normal
 * 200 page with a way onward (SRC-2); the typed text is shown as encoded text only (SRC-3).
 */
@Component({
  selector: 'rp-search-page',
  imports: [NotFoundComponent, BreadcrumbsComponent, ProductTileComponent, FilterPanelComponent, ActiveFiltersComponent, PaginationComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './search-page.container.html',
  styleUrl: './search-page.container.scss',
})
export class SearchPageContainer {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly queryMap = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly links = this.store.selectSignal(selectTopLinks);
  private readonly key = this.store.selectSignal(selectSearchKey);
  private readonly stored = this.store.selectSignal(selectSearchResult);

  protected readonly path = PATH;

  /** The query string as the visitor wrote it. */
  private readonly raw = computed(() => {
    const q = this.queryMap();
    const out: Record<string, string[]> = {};
    for (const k of q.keys) out[k] = q.getAll(k);
    const lo = out['priceMin']?.[0];
    const hi = out['priceMax']?.[0];
    if (!out['price'] && (lo !== undefined || hi !== undefined)) out['price'] = [`${lo ?? ''}-${hi ?? ''}`];
    return out;
  });

  protected readonly query = computed(() => normaliseQuery(this.raw()['q']?.[0]));
  protected readonly filter = computed(() => parseFilter(this.raw()));
  protected readonly extra = computed((): Readonly<Record<string, string>> => ({ q: this.query() }));
  private readonly currentKey = computed(() => searchKey(this.query(), this.filter()));

  /** The result for the request now on screen, or null while it loads (or when there is nothing to look for). */
  protected readonly result = computed((): SearchResult | null => (this.key() === this.currentKey() ? this.stored() : null));

  protected readonly page = computed(() => {
    const r = this.result();
    return r?.status === 'ok' ? r.page : null;
  });

  protected readonly crumbs = computed((): readonly Crumb[] => [{ label: 'Home', path: '/' }, { label: 'Search', path: null }]);

  /** One chip per active filter, each a link to the same search without it. */
  protected readonly chips = computed((): readonly FilterChip[] => {
    const f = this.filter();
    const options = this.page()?.options;
    const label = (list: readonly { value: string; label: string }[] | undefined, v: string) => list?.find((o) => o.value === v)?.label ?? v;
    const chips: FilterChip[] = [];
    for (const s of f.sizes) chips.push({ label: `Size: ${label(options?.sizes, s)}`, without: { ...f, sizes: f.sizes.filter((x) => x !== s), page: 1 } });
    for (const c of f.colours) chips.push({ label: `Colour: ${label(options?.colours, c)}`, without: { ...f, colours: f.colours.filter((x) => x !== c), page: 1 } });
    if (f.age) chips.push({ label: `Age: ${label(options?.ages, f.age)}`, without: { ...f, age: null, page: 1 } });
    if (f.priceMin !== null || f.priceMax !== null) {
      chips.push({ label: `Price: ${f.priceMin ?? 0} to ${f.priceMax ?? 'any'}`, without: { ...f, priceMin: null, priceMax: null, page: 1 } });
    }
    return chips;
  });

  protected readonly clearAll = computed((): ListingFilter => ({ ...this.filter(), sizes: [], colours: [], age: null, priceMin: null, priceMax: null, page: 1 }));

  constructor() {
    effect(() => {
      // One spelling per search: unknown keys, odd order and untidy text redirect to the clean address (SEO-06).
      const canonical = `q=${this.query()}${toQueryString(this.filter()) ? `&${toQueryString(this.filter())}` : ''}`;
      const asked = Object.entries(this.raw()).flatMap(([k, v]) => v.map((x) => `${k}=${x}`)).join('&');
      if (this.query() !== '' && canonical !== asked) {
        this.redirect(searchUrl(this.query(), this.filter()), 301);
        return;
      }

      if (this.query() === '') {
        // Nothing typed: the page is just the search form (no request).
        if (this.responseInit) this.responseInit.status = 200;
        this.describeEmpty();
        return;
      }

      this.store.dispatch(SearchResultsActions.load({ key: this.currentKey(), query: this.query(), filter: this.filter() }));
    });

    effect(() => {
      const r = this.result();
      if (!r) return;
      const siteName = this.brand().name;
      if (r.status === 'redirect') {
        this.redirect(r.url, 302);
        return;
      }

      if (this.responseInit) this.responseInit.status = r.status === 'ok' ? 200 : r.status === 'notFound' ? 404 : 503;
      if (r.status !== 'ok') {
        this.seo.set({ title: r.status === 'notFound' ? 'Page not found' : 'Something went wrong', description: 'This page is not available.', path: PATH, siteName, crumbs: [], noindex: true });
        return;
      }

      const page = r.page;
      // Result pages are for visitors: not indexed, links followed, canonical to the search itself without paging or filter noise (SRC-11).
      this.seo.set({
        title: `Results for “${page.query}”`,
        description: `${page.total} ${page.total === 1 ? 'piece' : 'pieces'} found for “${page.query}” at ${siteName}.`,
        path: `${PATH}?q=${encodeURIComponent(page.query)}`,
        siteName,
        crumbs: [{ name: 'Search', path: PATH }],
        noindex: true,
        follow: true,
      });
    });
  }

  /** The filter panel posts as GET without scripting; with scripting the router does the same navigation without a reload. */
  protected apply(filter: ListingFilter): void {
    void this.router.navigate([PATH], { queryParams: { q: this.query(), ...toCanonicalQuery(filter) } });
  }

  private describeEmpty(): void {
    this.seo.set({
      title: 'Search',
      description: `Search ${this.brand().name} by name, category, colour or code.`,
      path: PATH,
      siteName: this.brand().name,
      crumbs: [{ name: 'Search', path: PATH }],
      noindex: true,
      follow: true,
    });
  }

  private redirect(url: string, status: 301 | 302): void {
    if (this.responseInit) {
      this.responseInit.status = status;
      const headers = new Headers(this.responseInit.headers);
      headers.set('Location', url);
      this.responseInit.headers = headers;
    } else {
      void this.router.navigateByUrl(url, { replaceUrl: true });
    }
  }
}
