import { provideHttpClient, withFetch } from '@angular/common/http';
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
import { provideSearchSuggest } from './features/storefront/search-suggest';
import { provideSiteSettings } from './features/storefront/site-settings';
import { provideRpImageLoader } from './shared/ui/image/image-loader';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideClientHydration(),
    provideHttpClient(withFetch()),
    provideStore(),
    provideEffects(),
    provideSiteSettings(),
    provideNavigation(),
    provideListings(),
    provideProductPages(),
    provideSearchSuggest(),
    providePage(),
    provideRpImageLoader(),
  ],
};
