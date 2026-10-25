import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { NotFoundComponent } from '../../../../../shared/ui/not-found/not-found.component';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { BreadcrumbsComponent } from '../../../navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand, selectTopLinks } from '../../../site-settings';
import { AvailabilityNoteComponent } from '../../components/availability-note/availability-note.component';
import { ColourOptionsComponent } from '../../components/colour-options/colour-options.component';
import { CompositionCareComponent } from '../../components/composition-care/composition-care.component';
import { ProductGalleryComponent } from '../../components/product-gallery/product-gallery.component';
import { ProductPriceComponent } from '../../components/product-price/product-price.component';
import { SizeChartComponent } from '../../components/size-chart/size-chart.component';
import { SizeOptionsComponent } from '../../components/size-options/size-options.component';
import { SoldOutStateComponent } from '../../components/sold-out-state/sold-out-state.component';
import { ProductPageResult } from '../../models/product-page.model';
import { parseSelection, productKey, selectionQueryString } from '../../models/product-selection';
import { ProductPagesActions } from '../../store/product-pages.actions';
import { selectProductPageKey, selectProductPageResult } from '../../store/product-pages.selectors';

const META_DESCRIPTION_LENGTH = 155;

/**
 * One product page (FR-07..09). Server-rendered; the address decides the status: 200, a 301 to the product current address, 404 or 410
 * (PDP-13). The colour and size come from the query, so a choice works as a plain link and the page canonical stays the product itself
 * (Q-PDP-2). Meta and structured data beyond the basics belong to the seo feature.
 */
@Component({
  selector: 'rp-product-page',
  imports: [
    NotFoundComponent,
    BreadcrumbsComponent,
    ProductGalleryComponent,
    ProductPriceComponent,
    ColourOptionsComponent,
    SizeOptionsComponent,
    AvailabilityNoteComponent,
    SoldOutStateComponent,
    SizeChartComponent,
    CompositionCareComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './product-page.container.html',
  styleUrl: './product-page.container.scss',
})
export class ProductPageContainer {
  private readonly store = inject(Store);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly segments = toSignal(this.route.url.pipe(map((u) => u.map((s) => s.path))), { requireSync: true });
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly links = this.store.selectSignal(selectTopLinks);
  private readonly key = this.store.selectSignal(selectProductPageKey);
  private readonly stored = this.store.selectSignal(selectProductPageResult);

  protected readonly path = computed(() => '/' + this.segments().join('/'));
  protected readonly selection = computed(() => parseSelection({ colour: this.query().get('colour'), size: this.query().get('size') }));
  private readonly currentKey = computed(() => productKey(this.path(), this.selection()));

  /** The result for the request now on screen, or null while it loads. */
  protected readonly result = computed((): ProductPageResult | null => (this.key() === this.currentKey() ? this.stored() : null));

  protected readonly page = computed(() => {
    const r = this.result();
    return r?.status === 'found' ? r.page : null;
  });

  /** The price line. A sold-out product shows its price without the discount treatment (PR-4); the figures are the server ones. */
  protected readonly price = computed(() => {
    const p = this.page();
    if (!p?.price) {
      return null;
    }
    return p.isAvailable ? p.price : { ...p.price, compareAtPrice: null, savingPercent: 0, savingAmount: 0 };
  });

  /** The size the visitor chose, as the server confirmed it. */
  protected readonly chosenSize = computed(() => {
    const p = this.page();
    return p?.sizes.find((s) => s.code === p.selectedSize) ?? null;
  });

  constructor() {
    effect(() => {
      this.store.dispatch(ProductPagesActions.load({ key: this.currentKey(), path: this.path(), selection: this.selection() }));
    });

    effect(() => {
      const r = this.result();
      if (!r) return;
      const siteName = this.brand().name;
      if (this.responseInit) this.responseInit.status = this.statusOf(r);
      if (r.status === 'moved') {
        this.redirect(this.withSelection(r.path));
        return;
      }

      if (r.status !== 'found') {
        const title = r.status === 'error' ? 'Something went wrong' : r.status === 'gone' ? 'No longer available' : 'Page not found';
        this.seo.set({ title, description: 'This page is not available.', path: this.path(), siteName, crumbs: [], noindex: true });
        return;
      }

      const p = r.page;
      this.seo.set({
        title: p.name,
        description: this.describe(p.name, p.description),
        // One canonical per product: a colour or size in the query never makes a second indexable page (Q-PDP-2, SEO-06).
        path: p.path,
        siteName,
        crumbs: p.breadcrumbs.map((c, i, all) => ({ name: c.label, path: c.path ?? (i === all.length - 1 ? p.path : '/') })),
      });
    });
  }

  private statusOf(r: ProductPageResult): number {
    switch (r.status) {
      case 'found':
        return 200;
      case 'moved':
        return 301;
      case 'gone':
        return 410;
      case 'notFound':
        return 404;
      default:
        return 503;
    }
  }

  private describe(name: string, description: string): string {
    const text = (description.trim() ? `${name}. ${description.trim()}` : name).replace(/\s+/g, ' ');
    return text.length <= META_DESCRIPTION_LENGTH ? text : `${text.slice(0, META_DESCRIPTION_LENGTH - 1).trimEnd()}…`;
  }

  /** The redirect keeps the visitor choice. The target path is Catalog own; only the query is the visitor, and it is already clamped. */
  private withSelection(target: string): string {
    const query = selectionQueryString(this.selection());
    return query ? `${target}?${query}` : target;
  }

  private redirect(url: string): void {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Location', url);
      this.responseInit.headers = headers;
    } else {
      void this.router.navigateByUrl(url, { replaceUrl: true });
    }
  }
}
