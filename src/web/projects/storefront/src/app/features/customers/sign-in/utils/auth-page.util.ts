import { RESPONSE_INIT, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Store } from '@ngrx/store';
import { map } from 'rxjs';
import { CustomerAuthApi } from '../../../../core/customer-auth/services/customer-auth-api.service';
import { SeoService } from '../../../../shared/seo/seo.service';
import { selectBrand } from '../../../storefront/site-settings';

/**
 * What every sign-in screen shares (ACT-16): the page is `noindex, nofollow`, its response is never cached, and the Turnstile site key is
 * known. Call it from a component constructor (it needs the injection context).
 */
export function setUpAuthPage(page: { title: string; description: string; path: string }): { siteKey: () => string | null } {
  const seo = inject(SeoService);
  const brand = inject(Store).selectSignal(selectBrand);
  const responseInit = inject(RESPONSE_INIT, { optional: true });

  if (responseInit) {
    const headers = new Headers(responseInit.headers);
    headers.set('Cache-Control', 'private, no-store');
    responseInit.headers = headers;
  }

  effect(() => seo.set({ ...page, siteName: brand().name, crumbs: [], noindex: true }));

  // null until the answer is in, so the check is never started before the site key is known
  const siteKey = toSignal(inject(CustomerAuthApi).config().pipe(map((c): string | null => c.turnstileSiteKey)), { initialValue: null });
  return { siteKey };
}
