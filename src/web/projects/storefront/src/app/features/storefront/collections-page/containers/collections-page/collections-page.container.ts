import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { NotFoundComponent } from '../../../../../shared/ui/not-found/not-found.component';
import { EMPTY_FILTER, PaginationComponent } from '../../../listings';
import { BreadcrumbsComponent, Crumb } from '../../../navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand, selectTopLinks } from '../../../site-settings';
import { CollectionTileComponent } from '../../components/collection-tile/collection-tile.component';
import { CollectionsPageResult } from '../../models/collections-page.model';
import { CollectionsPageActions } from '../../store/collections-page.actions';
import { selectCollectionsKey, selectCollectionsResult } from '../../store/collections-page.selectors';

const PATH = '/collections';

/**
 * The collections index (collections-page spec, COL-1..8): every enabled collection that has live products, as picture tiles. Server-rendered
 * and indexable. A page with no qualifying collection is a normal 200 with a way onward (COL-6); a page after the last is a real 404; a
 * failure is a 503.
 */
@Component({
  selector: 'rp-collections-page',
  imports: [BreadcrumbsComponent, CollectionTileComponent, PaginationComponent, NotFoundComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './collections-page.container.html',
})
export class CollectionsPageContainer {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });
  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly links = this.store.selectSignal(selectTopLinks);
  private readonly key = this.store.selectSignal(selectCollectionsKey);
  private readonly stored = this.store.selectSignal(selectCollectionsResult);

  protected readonly path = PATH;
  protected readonly filter = EMPTY_FILTER;
  /** A text, zero or negative page means the first page (the same rule as the listings). */
  protected readonly page = computed(() => {
    const text = this.query().get('page') ?? '1';
    return /^[0-9]{1,6}$/.test(text) && Number(text) >= 1 ? Number(text) : 1;
  });
  private readonly currentKey = computed(() => String(this.page()));

  /** What to draw: the result for this page, or the last one while the next loads (never a blank flash). */
  protected readonly result = computed((): CollectionsPageResult | null => (this.key() === this.currentKey() ? this.stored() : null));
  protected readonly crumbs = computed((): readonly Crumb[] => [{ label: 'Home', path: '/' }, { label: 'Collections', path: null }]);

  constructor() {
    effect(() => this.store.dispatch(CollectionsPageActions.load({ key: this.currentKey(), page: this.page() })));

    effect(() => {
      const r = this.result();
      if (!r) return;
      const siteName = this.brand().name;
      if (r.status === 'error') {
        if (this.responseInit) this.responseInit.status = 503;
        this.seo.set({ title: 'Something went wrong', description: 'This page is not available.', path: PATH, siteName, crumbs: [], noindex: true, error: true });
        return;
      }

      const data = r.page;
      if (data.items.length === 0 && data.total > 0) {
        if (this.responseInit) this.responseInit.status = 404;
        this.seo.set({ title: 'Page not found', description: 'This page is not available.', path: PATH, siteName, crumbs: [], noindex: true, error: true });
        return;
      }

      if (this.responseInit) this.responseInit.status = 200;
      this.seo.set({
        title: 'Collections',
        description: `Browse the ${siteName} collections: seasonal edits and favourites, each with the pieces in it.`,
        path: data.page > 1 ? `${PATH}?page=${data.page}` : PATH,
        siteName,
        crumbs: [{ name: 'Collections', path: PATH }],
      });
    });
  }

  protected notFound(r: CollectionsPageResult): boolean {
    return r.status === 'ok' && r.page.items.length === 0 && r.page.total > 0;
  }
}
