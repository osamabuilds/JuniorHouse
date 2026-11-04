import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject } from '@angular/core';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { BreadcrumbsComponent, Crumb } from '../../../../storefront/navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { NewsletterActions } from '../../store/newsletter.actions';
import { selectNewsletterMine, selectNewsletterSaveFailed, selectNewsletterSaving, selectNewsletterSubscribed } from '../../store/newsletter.reducer';

const PATH = '/account/preferences';

/**
 * Account preferences (NLS-2, TC-ACC-17): the marketing-email switch for the signed-in customer's own address. The page is for one
 * person, so it is `noindex` and never cached. Transactional mail (orders, password reset) is not controlled here and never carries marketing.
 */
@Component({
  selector: 'rp-preferences-page',
  imports: [BreadcrumbsComponent, ButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './preferences-page.container.html',
  styleUrl: './preferences-page.container.scss',
})
export class PreferencesPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);

  protected readonly status = this.store.selectSignal(selectNewsletterMine);
  protected readonly subscribed = this.store.selectSignal(selectNewsletterSubscribed);
  protected readonly saving = this.store.selectSignal(selectNewsletterSaving);
  protected readonly failed = this.store.selectSignal(selectNewsletterSaveFailed);
  protected readonly crumbs: readonly Crumb[] = [{ label: 'Home', path: '/' }, { label: 'Account', path: '/account/addresses' }, { label: 'Preferences', path: null }];
  protected readonly note = computed(() => (this.failed() ? 'We could not save that. Please try again.' : this.saving() ? 'Saving...' : ''));

  constructor() {
    this.noCache();
    this.store.dispatch(NewsletterActions.loadMine());
    effect(() => {
      this.seo.set({
        title: 'Preferences',
        description: 'Your email preferences.',
        path: PATH,
        siteName: this.brand().name,
        crumbs: [{ name: 'Account', path: '/account/addresses' }, { name: 'Preferences', path: PATH }],
        noindex: true,
      });
    });
  }

  protected toggle(on: boolean): void {
    this.store.dispatch(NewsletterActions.setMine({ subscribed: on }));
  }

  protected reload(): void {
    this.store.dispatch(NewsletterActions.loadMine());
  }

  private noCache(): void {
    if (!this.responseInit) return;
    const headers = new Headers(this.responseInit.headers);
    headers.set('Cache-Control', 'private, no-store');
    this.responseInit.headers = headers;
  }
}
