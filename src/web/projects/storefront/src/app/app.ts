import { ChangeDetectionStrategy, Component, afterNextRender, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { Store } from '@ngrx/store';
import { filter, map } from 'rxjs';
import { CartActions } from './features/cart/carts/store/cart.actions';
import { NavigationActions } from './features/storefront/navigation';
import { SiteSettingsActions } from './features/storefront/site-settings';
import { AnnouncementBarComponent } from './layout/announcement-bar/announcement-bar.component';
import { VerifyEmailBarComponent } from './layout/verify-email-bar/verify-email-bar.component';
import { MiniCartContainer } from './features/cart/carts/containers/mini-cart/mini-cart.container';
import { MobileMenuComponent } from './layout/mobile-menu/mobile-menu.component';
import { SearchOverlayComponent } from './layout/search-overlay/search-overlay.component';
import { SiteFooterComponent } from './layout/site-footer/site-footer.component';
import { SiteHeaderComponent } from './layout/site-header/site-header.component';

@Component({
  imports: [RouterOutlet, RouterLink, MiniCartContainer, AnnouncementBarComponent, VerifyEmailBarComponent, SiteHeaderComponent, SiteFooterComponent, MobileMenuComponent, SearchOverlayComponent],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'rp-root' },
  templateUrl: './app.html',
})
export class App {
  private readonly router = inject(Router);

  /** The sign-in screens show a slim wordmark header and no footer (design system: AuthScreens); their routes carry `data: { shell: 'slim' }`. */
  protected readonly slim = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.isSlim()),
    ),
    { initialValue: this.isSlim() },
  );

  constructor() {
    const store = inject(Store);
    store.dispatch(SiteSettingsActions.load());
    store.dispatch(NavigationActions.loadMenu());
    // The bag count in the header comes from the browser's own cookies, so the basket is read there.
    afterNextRender(() => store.dispatch(CartActions.load()));
  }

  private isSlim(): boolean {
    let route = this.router.routerState.snapshot.root;
    while (route.firstChild) route = route.firstChild;
    return route.data['shell'] === 'slim';
  }
}
