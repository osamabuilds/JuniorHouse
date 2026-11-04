import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, RESPONSE_INIT, effect, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { BreadcrumbsComponent, Crumb } from '../../../../storefront/navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { WishlistHeartComponent } from '../../components/wishlist-heart/wishlist-heart.component';
import { WishlistItem } from '../../models/wishlist.model';
import { WishlistActions } from '../../store/wishlist.actions';
import {
  selectSavedIds,
  selectWishlistMessage,
  selectWishlistPage,
  selectWishlistSignedIn,
  selectWishlistStatus,
} from '../../store/wishlist.selectors';

const PATH = '/wishlist';

/**
 * The wishlist page (WSL-1, WSL-5, WSL-6, WSL-9). A signed-in customer sees their saved products with the price now, a sale or
 * price-drop note, and a sold-out or no-longer-available note; a signed-out shopper is told what is remembered on this device and asked to
 * sign in. The page is for one person, so it is `noindex` and its response is never cached (SEO-05, SEC-05).
 */
@Component({
  selector: 'rp-wishlist-page',
  imports: [BreadcrumbsComponent, ButtonDirective, DecimalPipe, RouterLink, WishlistHeartComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './wishlist-page.container.html',
  styleUrl: './wishlist-page.container.scss',
})
export class WishlistPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly router = inject(Router);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  protected readonly signedIn = this.store.selectSignal(selectWishlistSignedIn);
  protected readonly status = this.store.selectSignal(selectWishlistStatus);
  protected readonly page = this.store.selectSignal(selectWishlistPage);
  protected readonly message = this.store.selectSignal(selectWishlistMessage);
  protected readonly savedIds = this.store.selectSignal(selectSavedIds);
  private readonly brand = this.store.selectSignal(selectBrand);

  protected readonly crumbs: readonly Crumb[] = [{ label: 'Home', path: '/' }, { label: 'Wishlist', path: null }];

  constructor() {
    this.noCache();

    effect(() => {
      this.seo.set({
        title: 'Wishlist',
        description: 'The products you have saved.',
        path: PATH,
        siteName: this.brand().name,
        crumbs: [{ name: 'Wishlist', path: PATH }],
        noindex: true,
      });
    });

    // The list is read once the customer is known to be signed in (and again after a sign-out and sign-in).
    effect(() => {
      if (this.signedIn() === true && (this.status() === 'idle' || this.status() === 'signedOut')) this.store.dispatch(WishlistActions.loadPage({ page: 1 }));
    });
  }

  protected here(): string {
    return this.router.url;
  }

  protected goTo(page: number): void {
    this.store.dispatch(WishlistActions.loadPage({ page }));
  }

  protected reload(): void {
    this.store.dispatch(WishlistActions.loadPage({ page: 1 }));
  }

  protected has(item: WishlistItem, note: string): boolean {
    return item.notes.includes(note as WishlistItem['notes'][number]);
  }

  /** The saving against the struck-through price, from server amounts only (SEC-09). */
  protected saving(item: WishlistItem): number {
    return item.price !== null && item.wasPrice !== null && item.wasPrice > item.price ? item.wasPrice - item.price : 0;
  }

  protected percent(item: WishlistItem): number {
    return item.price !== null && item.wasPrice !== null && item.wasPrice > 0 ? Math.round((this.saving(item) / item.wasPrice) * 100) : 0;
  }

  protected remove(item: WishlistItem): void {
    this.store.dispatch(WishlistActions.remove({ productId: item.productId }));
  }

  /** One customer's data: no shared cache or browser history may keep the page (NFR-26). */
  private noCache(): void {
    if (!this.responseInit) return;
    const headers = new Headers(this.responseInit.headers);
    headers.set('Cache-Control', 'private, no-store');
    this.responseInit.headers = headers;
  }
}
