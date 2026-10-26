import { Provider, Type } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Action, Store, provideStore } from '@ngrx/store';
import { StorefrontSettingsApiActions } from '@features/storefront/storefront-settings/store/storefront-settings.actions';
import { storefrontSettingsReducer } from '@features/storefront/storefront-settings/store/storefront-settings.reducer';
import { STOREFRONT_SETTINGS_FEATURE_KEY } from '@features/storefront/storefront-settings/store/storefront-settings.state';
import { DraftSettings } from '@features/storefront/storefront-settings/models';
import { draftSettings } from './storefront-fixtures';
import { provideAllowAllPermissions } from './provide-allow-all-permissions';

/**
 * A real storefront-settings store with the actions it receives recorded, for container specs. The draft is loaded the way the effect
 * would load it, so the container starts from a saved draft.
 */
export function createStorefrontContainer<T>(component: Type<T>, options: { draft?: DraftSettings | null; routes?: never[]; providers?: Provider[] } = {}) {
  TestBed.configureTestingModule({
    providers: [provideAllowAllPermissions(), provideRouter(options.routes ?? []), provideStore({ [STOREFRONT_SETTINGS_FEATURE_KEY]: storefrontSettingsReducer }), ...(options.providers ?? [])],
  });
  const store = TestBed.inject(Store);
  const dispatched: Action[] = [];
  const original = store.dispatch.bind(store);
  store.dispatch = ((action: Action) => {
    dispatched.push(action);
    return original(action as never);
  }) as typeof store.dispatch;

  const fixture: ComponentFixture<T> = TestBed.createComponent(component);
  const draft = options.draft === undefined ? draftSettings() : options.draft;
  if (draft) {
    store.dispatch(StorefrontSettingsApiActions.loadDraftSucceeded({ draft }));
  }

  fixture.detectChanges();
  const root = fixture.nativeElement as HTMLElement;
  const last = (type: string) => [...dispatched].reverse().find((a) => a.type === type);
  return { fixture, root, store, dispatched, last, instance: fixture.componentInstance };
}
