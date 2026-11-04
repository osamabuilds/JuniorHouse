import { TestBed } from '@angular/core/testing';
import { provideEffects } from '@ngrx/effects';
import { Store, provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { MineResult, SubscribeResult } from '../models/newsletter.model';
import { NewsletterApi } from '../services/newsletter-api.service';
import { NewsletterActions } from './newsletter.actions';
import { NewsletterEffects } from './newsletter.effects';
import { newsletterFeature, selectNewsletterFooter, selectNewsletterMine, selectNewsletterSaveFailed, selectNewsletterSaving, selectNewsletterSubscribed } from './newsletter.reducer';

describe('newsletter store', () => {
  const subscribe = vi.fn<(email: string) => SubscribeResult>();
  const mine = vi.fn<() => MineResult>();
  const setMine = vi.fn<(on: boolean) => MineResult>();

  function store(): Store {
    TestBed.configureTestingModule({
      providers: [
        provideStore({ [newsletterFeature.name]: newsletterFeature.reducer }),
        provideEffects(NewsletterEffects),
        { provide: NewsletterApi, useValue: { subscribe: (e: string) => of(subscribe(e)), mine: () => of(mine()), setMine: (on: boolean) => of(setMine(on)) } },
      ],
    });
    return TestBed.inject(Store);
  }
  const read = <T>(s: Store, selector: Parameters<Store['selectSignal']>[0]) => s.selectSignal(selector as never)() as T;

  beforeEach(() => {
    subscribe.mockReset().mockReturnValue('ok');
    mine.mockReset().mockReturnValue({ status: 'ok', subscribed: false });
    setMine.mockReset().mockImplementation((on) => ({ status: 'ok', subscribed: on }));
  });

  it('NLS-1 a footer subscribe ends done, and sends the email as typed', () => {
    const s = store();
    s.dispatch(NewsletterActions.subscribe({ email: 'sara@example.com' }));
    expect(subscribe).toHaveBeenCalledWith('sara@example.com');
    expect(read(s, selectNewsletterFooter)).toBe('done');
  });

  it.each(['invalid', 'busy', 'error'] as const)('NLS-6 a %s answer is kept so the form can say so', (answer) => {
    subscribe.mockReturnValue(answer);
    const s = store();
    s.dispatch(NewsletterActions.subscribe({ email: 'x' }));
    expect(read(s, selectNewsletterFooter)).toBe(answer);
  });

  it('typing again after an answer resets the form status', () => {
    const s = store();
    s.dispatch(NewsletterActions.subscribe({ email: 'sara@example.com' }));
    s.dispatch(NewsletterActions.resetFooter());
    expect(read(s, selectNewsletterFooter)).toBe('idle');
  });

  it('NLS-2 the preferences page loads the current choice', () => {
    mine.mockReturnValue({ status: 'ok', subscribed: true });
    const s = store();
    s.dispatch(NewsletterActions.loadMine());
    expect(read(s, selectNewsletterMine)).toBe('ready');
    expect(read(s, selectNewsletterSubscribed)).toBe(true);
  });

  it('NLS-2 a signed-out caller and a failure are told apart', () => {
    mine.mockReturnValue({ status: 'signedOut' });
    const s = store();
    s.dispatch(NewsletterActions.loadMine());
    expect(read(s, selectNewsletterMine)).toBe('signedOut');
    mine.mockReturnValue({ status: 'error' });
    s.dispatch(NewsletterActions.loadMine());
    expect(read(s, selectNewsletterMine)).toBe('error');
  });

  it('NLS-2 switching on then off keeps the order and ends with the last choice', () => {
    const s = store();
    s.dispatch(NewsletterActions.setMine({ subscribed: true }));
    s.dispatch(NewsletterActions.setMine({ subscribed: false }));
    expect(setMine.mock.calls.map((c) => c[0])).toEqual([true, false]);
    expect(read(s, selectNewsletterSubscribed)).toBe(false);
    expect(read(s, selectNewsletterSaving)).toBe(false);
  });

  it('NLS-2 a failed change is reported and does not change the choice shown', () => {
    setMine.mockReturnValue({ status: 'error' });
    const s = store();
    s.dispatch(NewsletterActions.setMine({ subscribed: true }));
    expect(read(s, selectNewsletterSaveFailed)).toBe(true);
    expect(read(s, selectNewsletterSubscribed)).toBe(false);
  });
});
