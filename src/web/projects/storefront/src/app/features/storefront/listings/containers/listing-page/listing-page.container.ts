import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { BreadcrumbsComponent, Crumb } from '../../../navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand, selectTopLinks } from '../../../site-settings';
import { ListingResult } from '../../models/listing.model';
import { ListingFilter, ListingScope, ListingScopeKind, listingKey, parseFilter, scopePath, toCanonicalQuery, toQueryString } from '../../models/listing-query';
import { ActiveFiltersComponent, FilterChip } from '../../components/active-filters/active-filters.component';
import { EmptyStateComponent } from '../../components/empty-state/empty-state.component';
import { FilterPanelComponent } from '../../components/filter-panel/filter-panel.component';
import { PaginationComponent } from '../../components/pagination/pagination.component';
import { ProductTileComponent } from '../../components/product-tile/product-tile.component';
import { ListingsActions } from '../../store/listings.actions';
import { selectListingKey, selectListingResult } from '../../store/listings.selectors';


/** One listing: New in, Sale, an audience, a category or a collection (FR-02, FR-03, FR-05). Server-rendered, with real 404 and 301 statuses. */
@Component({
  selector: 'rp-listing-page',
  imports: [RouterLink, BreadcrumbsComponent, ProductTileComponent, FilterPanelComponent, ActiveFiltersComponent, PaginationComponent, EmptyStateComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './listing-page.container.html',
  styleUrl: './listing-page.container.scss',
})
export class ListingPageContainer {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly params = toSignal(this.route.paramMap, { requireSync: true });
  private readonly queryMap = toSignal(this.route.queryParamMap, { requireSync: true });
  private readonly data = toSignal(this.route.data.pipe(map((d) => d as { kind?: ListingScopeKind })), { requireSync: true });

  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly links = this.store.selectSignal(selectTopLinks);
  private readonly key = this.store.selectSignal(selectListingKey);
  private readonly stored = this.store.selectSignal(selectListingResult);

  protected readonly scope = computed((): ListingScope => {
    const p = this.params();
    return {
      kind: this.data().kind ?? 'audience',
      ...(p.get('gender') ? { gender: p.get('gender')! } : {}),
      ...(p.get('category') ? { category: p.get('category')! } : {}),
      ...(p.get('code') ? { collection: p.get('code')! } : {}),
    };
  });

  /** The query as the visitor wrote it, with the legacy no-script price boxes folded into one price range. */
  private readonly raw = computed(() => {
    const q = this.queryMap();
    const out: Record<string, string[]> = {};
    for (const k of q.keys) out[k] = q.getAll(k);
    const lo = out['priceMin']?.[0];
    const hi = out['priceMax']?.[0];
    if (!out['price'] && (lo !== undefined || hi !== undefined)) out['price'] = [`${lo ?? ''}-${hi ?? ''}`];
    return out;
  });

  protected readonly filter = computed(() => parseFilter(this.raw()));
  protected readonly path = computed(() => scopePath(this.scope()));
  private readonly currentKey = computed(() => listingKey(this.scope(), this.filter()));

  /** The result for the request now on screen, or null while it loads. */
  protected readonly result = computed((): ListingResult | null => (this.key() === this.currentKey() ? this.stored() : null));

  protected readonly page = computed(() => {
    const r = this.result();
    return r?.status === 'ok' ? r.page : null;
  });

  protected readonly crumbs = computed((): readonly Crumb[] => this.page()?.breadcrumbs ?? []);


  /** One chip per active filter, each a link to the same listing without it (LST-3, 6). */
  protected readonly chips = computed((): readonly FilterChip[] => {
    const f = this.filter();
    const r = this.result();
    const options = r?.status === 'ok' ? r.options : null;
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
      // Redirect to the one canonical spelling of the query: unknown keys and odd ordering never make a second URL (SEO-06).
      const canonical = toQueryString(this.filter());
      const asked = Object.entries(this.raw()).flatMap(([k, v]) => v.map((x) => `${k}=${x}`)).join('&');
      if (canonical !== asked) {
        this.redirect(canonical ? `${this.path()}?${canonical}` : this.path());
        return;
      }
      this.store.dispatch(ListingsActions.load({ key: this.currentKey(), scope: this.scope(), filter: this.filter() }));
    });

    effect(() => {
      const r = this.result();
      if (!r) return;
      const siteName = this.brand().name;
      if (this.responseInit) this.responseInit.status = r.status === 'ok' ? 200 : r.status === 'notFound' ? 404 : 503;
      if (r.status !== 'ok') {
        this.seo.set({ title: r.status === 'notFound' ? 'Page not found' : 'Something went wrong', description: 'This page is not available.', path: this.path(), siteName, crumbs: [], noindex: true });
        return;
      }
      const page = r.page;
      // Paginated pages are self-canonical (Q-SEO-2); filter and sort variants point at the base listing (SEO-06). Empty results are not indexed (LST-5).
      const path = page.page > 1 ? `${this.path()}?page=${page.page}` : this.path();
      const saving = page.maxSavingPercent > 0 ? ` Up to ${page.maxSavingPercent}% off.` : '';
      this.seo.set({
        title: page.page > 1 ? `${page.title}, page ${page.page}` : page.title,
        description: `Shop ${page.title.toLowerCase()} at ${siteName}: ${page.total} ${page.total === 1 ? 'piece' : 'pieces'}.${saving}`,
        path,
        siteName,
        crumbs: page.breadcrumbs.map((c, i, all) => ({ name: c.label, path: c.path ?? (i === all.length - 1 ? this.path() : '/') })),
        noindex: page.total === 0,
      });
    });
  }

  /** The filter panel posts as GET without scripting; with scripting the router does the same navigation without a reload. */
  protected apply(filter: ListingFilter): void {
    void this.router.navigate([this.path()], { queryParams: toCanonicalQuery(filter) });
  }

  private redirect(url: string): void {
    if (this.responseInit) {
      this.responseInit.status = 301;
      const headers = new Headers(this.responseInit.headers);
      headers.set('Location', url);
      this.responseInit.headers = headers;
    } else {
      void this.router.navigateByUrl(url, { replaceUrl: true });
    }
  }
}
