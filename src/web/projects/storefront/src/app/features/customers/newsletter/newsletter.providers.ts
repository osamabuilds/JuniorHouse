import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { NewsletterEffects } from './store/newsletter.effects';
import { newsletterFeature } from './store/newsletter.reducer';

/** Provided at the app root: the footer form is on every page. */
export function provideNewsletter(): EnvironmentProviders {
  return makeEnvironmentProviders([provideState(newsletterFeature), provideEffects(NewsletterEffects)]);
}
