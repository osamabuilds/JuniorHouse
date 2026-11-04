import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { NotifyResult } from '../models/notify-me.model';
import { NotifyMeApi } from '../services/notify-me-api.service';
import { NotifyMeActions } from './notify-me.actions';
import { NotifyMeEffects } from './notify-me.effects';
import { notifyMeFeature } from './notify-me.reducer';

describe('Notify Me store', () => {
  const request = vi.fn<(variantId: number, email: string) => NotifyResult>();

  function setup(): Store {
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [notifyMeFeature.name]: notifyMeFeature.reducer }),
        provideEffects(NotifyMeEffects),
        { provide: NotifyMeApi, useValue: { request: (v: number, e: string) => of(request(v, e)) } },
      ],
    });
    return TestBed.inject(Store);
  }
  const forms = (store: Store) => store.selectSignal(notifyMeFeature.selectForms)();

  beforeEach(() => request.mockReset().mockReturnValue({ status: 'ok' }));

  it('NTM-2 a request ends as done for that size', () => {
    const store = setup();
    store.dispatch(NotifyMeActions.request({ variantId: 7, email: 'sara@example.com' }));
    expect(request).toHaveBeenCalledWith(7, 'sara@example.com');
    expect(forms(store)[7]).toEqual({ status: 'done' });
  });

  it('NTM-6 an invalid answer keeps the server message for that size only', () => {
    request.mockReturnValue({ status: 'invalid', message: 'That size is in stock.' });
    const store = setup();
    store.dispatch(NotifyMeActions.request({ variantId: 7, email: 'a@b.co' }));
    expect(forms(store)[7]).toEqual({ status: 'invalid', message: 'That size is in stock.' });
    expect(forms(store)[8]).toBeUndefined();
  });

  it('NTM-3 two sizes keep their own state', () => {
    const store = setup();
    store.dispatch(NotifyMeActions.request({ variantId: 7, email: 'a@b.co' }));
    request.mockReturnValue({ status: 'busy' });
    store.dispatch(NotifyMeActions.request({ variantId: 8, email: 'a@b.co' }));
    expect(forms(store)[7]?.status).toBe('done');
    expect(forms(store)[8]?.status).toBe('busy');
  });

  it('NTM-10 reset clears one size', () => {
    const store = setup();
    store.dispatch(NotifyMeActions.request({ variantId: 7, email: 'a@b.co' }));
    store.dispatch(NotifyMeActions.reset({ variantId: 7 }));
    expect(forms(store)[7]).toBeUndefined();
  });
});
