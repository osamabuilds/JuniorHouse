import { provideHttpClient, withFetch } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideStore } from '@ngrx/store';
import { routes } from './app.routes';
import { provideClientHydration } from '@angular/platform-browser';
import { providePage } from './features/storefront/page/page.providers';
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
    provideSearchSuggest(),
    providePage(),
    provideRpImageLoader(),
  ],
};
