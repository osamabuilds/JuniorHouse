import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Store } from '@ngrx/store';
import { NavigationActions } from './features/storefront/navigation';
import { SiteSettingsActions } from './features/storefront/site-settings';
import { AnnouncementBarComponent } from './layout/announcement-bar/announcement-bar.component';
import { MobileMenuComponent } from './layout/mobile-menu/mobile-menu.component';
import { SearchOverlayComponent } from './layout/search-overlay/search-overlay.component';
import { SiteFooterComponent } from './layout/site-footer/site-footer.component';
import { SiteHeaderComponent } from './layout/site-header/site-header.component';

@Component({
  imports: [RouterOutlet, AnnouncementBarComponent, SiteHeaderComponent, SiteFooterComponent, MobileMenuComponent, SearchOverlayComponent],
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'rp-root' },
  templateUrl: './app.html',
})
export class App {
  constructor() {
    const store = inject(Store);
    store.dispatch(SiteSettingsActions.load());
    store.dispatch(NavigationActions.loadMenu());
  }
}
