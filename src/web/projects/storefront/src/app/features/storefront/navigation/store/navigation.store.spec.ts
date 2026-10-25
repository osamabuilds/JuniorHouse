import { vi } from 'vitest';
import { TransferState } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { Action } from '@ngrx/store';
import { Observable, of, throwError } from 'rxjs';
import { NavigationApi } from '../services/navigation-api.service';
import { sampleMenu } from '../testing/sample-menu';
import { NavigationActions } from './navigation.actions';
import { NavigationEffects } from './navigation.effects';
import { initialNavigationState, navigationReducer } from './navigation.reducer';
import { selectMenuEntries } from './navigation.selectors';

describe('navigation reducer and selectors', () => {
  it('NAV-2 starts with no entries rather than a made-up menu', () => {
    expect(selectMenuEntries({ navigation: initialNavigationState } as never)).toEqual([]);
  });

  it('NAV-5 the entries follow the menu the server returned', () => {
    const state = navigationReducer(initialNavigationState, NavigationActions.loadMenuSuccess({ menu: sampleMenu }));
    expect(selectMenuEntries({ navigation: state } as never).map((i) => i.label)).toEqual(['Girls', 'Boys', 'New in', 'Collections', 'Sale']);
  });

  it('keeps the last good menu when a later load fails', () => {
    let s = navigationReducer(initialNavigationState, NavigationActions.loadMenuSuccess({ menu: sampleMenu }));
    s = navigationReducer(s, NavigationActions.loadMenuFailure({ error: 'down' }));
    expect(s.menu).toBe(sampleMenu);
    expect(s.status).toBe('failed');
  });
});

describe('NavigationEffects', () => {
  function setup(api: Partial<NavigationApi>, transferred?: typeof sampleMenu) {
    let actions$: Observable<Action> = of(NavigationActions.loadMenu());
    TestBed.configureTestingModule({
      providers: [NavigationEffects, provideMockActions(() => actions$), { provide: NavigationApi, useValue: api }],
    });
    if (transferred) TestBed.inject(TransferState).set(NavigationEffects.TRANSFER_KEY, transferred);
    return TestBed.inject(NavigationEffects);
  }

  it('NAV-2 loads the menu and hands it to the browser through TransferState', async () => {
    const effects = setup({ getMenu: () => of(sampleMenu) });
    const action = await new Promise<Action>((resolve) => effects.loadMenu$.subscribe(resolve));
    expect(action).toEqual(NavigationActions.loadMenuSuccess({ menu: sampleMenu }));
    expect(TestBed.inject(TransferState).get(NavigationEffects.TRANSFER_KEY, null)).toBe(sampleMenu);
  });

  it('NAV-2 the browser uses the transferred menu and does not fetch again', async () => {
    const getMenu = vi.fn(() => of(sampleMenu));
    const effects = setup({ getMenu }, sampleMenu);
    const action = await new Promise<Action>((resolve) => effects.loadMenu$.subscribe(resolve));
    expect(action).toEqual(NavigationActions.loadMenuSuccess({ menu: sampleMenu }));
    expect(getMenu).not.toHaveBeenCalled();
  });

  it('a failed request becomes a failure action, not an exception', async () => {
    const effects = setup({ getMenu: () => throwError(() => new Error('503')) });
    const action = await new Promise<Action>((resolve) => effects.loadMenu$.subscribe(resolve));
    expect(action).toEqual(NavigationActions.loadMenuFailure({ error: '503' }));
  });
});
