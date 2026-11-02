import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { routes } from './app.routes';
import { provideClientHydration } from '@angular/platform-browser';
import { providePage } from './features/storefront/page/page.providers';
import { provideListings } from './features/storefront/listings';
import { provideProductPages } from './features/storefront/product-pages';
import { provideNavigation } from './features/storefront/navigation';
import { provideSearch } from './features/storefront/search';
import { provideSiteSettings } from './features/storefront/site-settings';
import { customerRefreshInterceptor, provideCustomerAuth, serverCookieInterceptor } from './core/customer-auth';
import { provideRpImageLoader } from './shared/ui/image/image-loader';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(),
    // Browser: renew an expired customer session once and repeat the call. Server render: forward the browser's cookies to the customer API.
    provideHttpClient(withFetch(), withInterceptors([customerRefreshInterceptor, serverCookieInterceptor])),
    provideStore(),
    provideEffects(),
    provideCustomerAuth(),
    provideSiteSettings(),
    provideNavigation(),
    provideListings(),
    provideProductPages(),
    provideSearch(),
    providePage(),
    provideRpImageLoader(),
  ],
};
