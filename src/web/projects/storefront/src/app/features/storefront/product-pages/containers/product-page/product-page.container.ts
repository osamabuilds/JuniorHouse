import { isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, ElementRef, PLATFORM_ID, RESPONSE_INIT, computed, effect, inject, signal, viewChild } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { WishlistHeartComponent } from '../../../../customers/wishlist/components/wishlist-heart/wishlist-heart.component';
import { NotifyMeFormComponent } from '../../../../customers/notify-me/components/notify-me-form/notify-me-form.component';
import { NotFoundComponent } from '../../../../../shared/ui/not-found/not-found.component';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { CartActions } from '../../../../cart/carts/store/cart.actions';
import { selectCartBusy, selectCartMessage } from '../../../../cart/carts/store/cart.selectors';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { SITE_URL, SeoService } from '../../../../../shared/seo/seo.service';
import { productStructuredData } from '../../utils/product-structured-data.util';
import { BreadcrumbsComponent } from '../../../navigation/components/breadcrumbs/breadcrumbs.component';
import { watchHeight, watchInView, watchMedia } from '../../../../../shared/utils/viewport';
import { selectBrand, selectFacts, selectTopLinks } from '../../../site-settings';
import { AvailabilityNoteComponent } from '../../components/availability-note/availability-note.component';
import { BuyBarComponent } from '../../components/buy-bar/buy-bar.component';
import { ColourOptionsComponent } from '../../components/colour-options/colour-options.component';
import { CompositionCareComponent } from '../../components/composition-care/composition-care.component';
import { ProductGalleryComponent } from '../../components/product-gallery/product-gallery.component';
import { ProductPerksComponent } from '../../components/product-perks/product-perks.component';
import { ProductPriceComponent } from '../../components/product-price/product-price.component';
import { SizeChartComponent } from '../../components/size-chart/size-chart.component';
import { SizeOptionsComponent } from '../../components/size-options/size-options.component';
import { SoldOutStateComponent } from '../../components/sold-out-state/sold-out-state.component';
import { ProductPageResult, SizeOption } from '../../models/product-page.model';
import { buildPerks } from '../../utils/perks.util';
import { parseSelection, productKey, selectionQueryString } from '../../models/product-selection';
import { ProductPagesActions } from '../../store/product-pages.actions';
import { ProductRecommendationsComponent } from '../../components/product-recommendations/product-recommendations.component';
import { NO_RECOMMENDATIONS, ProductRecommendations, RecommendationRow } from '../../models/product-recommendations.model';
import { ProductRecommendationsActions } from '../../store/product-recommendations.actions';
import { selectRecommendationsKey, selectRecommendationsResult } from '../../store/product-recommendations.selectors';
import { buildEventBody, sendRecommendationEvent } from '../../utils/recommendation-event.util';
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
    ButtonDirective,
    IconComponent,
    BreadcrumbsComponent,
    ProductGalleryComponent,
    ProductPriceComponent,
    ColourOptionsComponent,
    SizeOptionsComponent,
    AvailabilityNoteComponent,
    SoldOutStateComponent,
    SizeChartComponent,
    CompositionCareComponent,
    WishlistHeartComponent,
    NotifyMeFormComponent,
    BuyBarComponent,
    ProductPerksComponent,
    ProductRecommendationsComponent,
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
  private readonly site = inject(SITE_URL);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly segments = toSignal(this.route.url.pipe(map((u) => u.map((s) => s.path))), { requireSync: true });
  private readonly query = toSignal(this.route.queryParamMap, { requireSync: true });

  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly links = this.store.selectSignal(selectTopLinks);
  private readonly facts = this.store.selectSignal(selectFacts);
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

  // The two recommendation rows (REC-1, REC-4). Loaded for the address alone, in parallel with the product, and never held up by it: a
  // failed or slow call leaves the page complete without them (REC-10).
  private readonly recommendationsKey = this.store.selectSignal(selectRecommendationsKey);
  private readonly recommendationsStored = this.store.selectSignal(selectRecommendationsResult);
  protected readonly recommendations = computed((): ProductRecommendations =>
    this.recommendationsKey() === this.path() ? (this.recommendationsStored() ?? NO_RECOMMENDATIONS) : NO_RECOMMENDATIONS);

  /** A card of a recommendation row was opened. Counted in the browser only, by the row it came from (REC-12). */
  protected recommendationOpened(row: RecommendationRow, productId: number): void {
    this.countRecommendation(row, productId, 'click');
  }

  /** A size was added to the bag from a card of a row. */
  protected recommendationAdded(row: RecommendationRow, productId: number): void {
    this.countRecommendation(row, productId, 'add');
  }

  private countRecommendation(row: RecommendationRow, productId: number, action: 'click' | 'add'): void {
    if (!this.browser) return;
    sendRecommendationEvent(buildEventBody(row, productId, action), {
      sendBeacon: typeof navigator !== 'undefined' && 'sendBeacon' in navigator ? (url, data) => navigator.sendBeacon(url, data) : undefined,
      fetch: typeof fetch === 'function' ? (url, init) => fetch(url, init) : undefined,
    });
  }

  protected readonly bagBusy = this.store.selectSignal(selectCartBusy);
  protected readonly bagMessage = this.store.selectSignal(selectCartMessage);

  /** The size the visitor chose, as the server confirmed it. */
  protected readonly chosenSize = computed(() => {
    const p = this.page();
    return p?.sizes.find((s) => s.code === p.selectedSize) ?? null;
  });

  /** Only a size in stock can be bought. A sold-out size can be chosen (to ask to be told when it is back, NTM-1) but not added. */
  private canBuy(size: SizeOption): boolean {
    return size.selectable && (size.availability === 'InStock' || size.availability === 'LowStock') && size.variantId > 0;
  }

  /** Add to bag is offered once a size that can be bought is chosen; the server decides stock again when it is pressed (CRT-4). */
  protected readonly canAdd = computed(() => {
    const size = this.chosenSize();
    return !!size && this.canBuy(size);
  });

  /** Delivery, exchange and WhatsApp lines from the settings; a missing fact leaves its line out (BUY-10, BUY-11). */
  protected readonly perks = computed(() => buildPerks(this.facts()));

  // The bottom buy bar (BUY-1..9): below 64rem, while the main Add to bag button is off screen. It is created only in the browser.
  private readonly addArea = viewChild<ElementRef<HTMLElement>>('addArea');
  private readonly sizesArea = viewChild('sizesArea', { read: ElementRef });
  private readonly barHost = viewChild(BuyBarComponent, { read: ElementRef });
  private readonly mainAddVisible = signal(true);
  private readonly narrow = signal(false);
  private readonly pressedWithoutSize = signal(false);

  protected readonly barVisible = computed(() => this.page()?.isAvailable === true && this.narrow() && !this.mainAddVisible());
  /** A chosen size that cannot be bought blocks the bar exactly as it blocks the main button (BUY-5). */
  protected readonly barBlocked = computed(() => !!this.chosenSize() && !this.canAdd());
  protected readonly barHint = computed(() => (this.barBlocked() || this.pressedWithoutSize() ? this.addHint() : null));

  protected readonly addHint = computed(() => (this.chosenSize() ? 'That size is not available right now.' : 'Choose a size to add this to your bag.'));

  /** The bar's button: add when a size can be bought; otherwise take the visitor to the sizes and say why (BUY-3). */
  protected barAdd(): void {
    if (this.canAdd()) {
      this.pressedWithoutSize.set(false);
      this.addToBag();
      return;
    }
    if (this.barBlocked()) return;
    this.pressedWithoutSize.set(true);
    const sizes = this.sizesArea()?.nativeElement as HTMLElement | undefined;
    sizes?.scrollIntoView?.({ block: 'center', behavior: 'smooth' });
    sizes?.querySelector<HTMLElement>('a, button')?.focus({ preventScroll: true });
  }

  protected addToBag(): void {
    const size = this.chosenSize();
    if (size && this.canBuy(size)) this.store.dispatch(CartActions.add({ variantId: size.variantId, quantity: 1 }));
  }

  constructor() {
    effect((onCleanup) => {
      const el = this.addArea()?.nativeElement;
      if (el) onCleanup(watchInView(el, (visible) => this.mainAddVisible.set(visible)));
    });
    effect((onCleanup) => onCleanup(watchMedia('(max-width: 63.99rem)', (matches) => this.narrow.set(matches))));
    // While the bar shows, a focused field is never hidden behind it and the page end is not covered (BUY-7, WCAG 2.4.11).
    effect((onCleanup) => {
      const bar = (this.barHost()?.nativeElement as HTMLElement | undefined)?.querySelector('.rp-buybar');
      if (!bar) return;
      const root = document.documentElement;
      onCleanup(
        watchHeight(bar, (height) => {
          root.style.scrollPaddingBottom = height + 'px';
          document.body.style.paddingBottom = height + 'px';
        }),
      );
      onCleanup(() => {
        root.style.scrollPaddingBottom = '';
        document.body.style.paddingBottom = '';
      });
    });
    effect(() => {
      if (this.canAdd()) this.pressedWithoutSize.set(false);
    });
    effect(() => {
      this.store.dispatch(ProductPagesActions.load({ key: this.currentKey(), path: this.path(), selection: this.selection() }));
    });
    // Keyed by the path only, so choosing a colour or size does not fetch the rows again.
    effect(() => {
      this.store.dispatch(ProductRecommendationsActions.load({ key: this.path() }));
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
        this.seo.set({ title, description: 'This page is not available.', path: this.path(), siteName, crumbs: [], noindex: true, error: true });
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
        structured: [productStructuredData(p, this.site)],
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
