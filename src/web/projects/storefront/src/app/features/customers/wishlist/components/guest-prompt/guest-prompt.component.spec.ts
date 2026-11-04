import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Store, provideState, provideStore } from '@ngrx/store';
import { polyfillDialog } from '../../../../../layout/dialog/dialog.testing';
import { WishlistActions } from '../../store/wishlist.actions';
import { wishlistFeature } from '../../store/wishlist.reducer';
import { WishlistGuestPromptComponent } from './guest-prompt.component';

describe('WishlistGuestPromptComponent', () => {
  function setup() {
    polyfillDialog();
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideStore({}), provideState(wishlistFeature)] });
    const fixture = TestBed.createComponent(WishlistGuestPromptComponent);
    fixture.detectChanges();
    return { fixture, el: fixture.nativeElement as HTMLElement, store: TestBed.inject(Store) };
  }

  it('WSL-2 stays closed until a signed-out shopper presses a heart, then offers sign in and register', () => {
    const { fixture, el, store } = setup();
    const dialog = el.querySelector('dialog')!;
    expect(dialog.hasAttribute('open')).toBe(false);

    store.dispatch(WishlistActions.sessionKnown({ signedIn: false }));
    store.dispatch(WishlistActions.toggle({ productId: 7 }));
    fixture.detectChanges();
    TestBed.flushEffects?.();
    fixture.detectChanges();

    expect(dialog.hasAttribute('open')).toBe(true);
    expect(el.querySelector('h2')!.textContent).toContain('Keep your wishlist');
    const links = Array.from(el.querySelectorAll('a')).map((a) => a.getAttribute('href') ?? '');
    expect(links.some((h) => h.startsWith('/account/sign-in'))).toBe(true);
    expect(links.some((h) => h.startsWith('/account/register'))).toBe(true);
  });

  it('WSL-2 closing it tells the store, so the next press can ask again', () => {
    const { fixture, el, store } = setup();
    store.dispatch(WishlistActions.sessionKnown({ signedIn: false }));
    store.dispatch(WishlistActions.toggle({ productId: 7 }));
    fixture.detectChanges();
    TestBed.flushEffects?.();
    fixture.detectChanges();

    Array.from(el.querySelectorAll('button')).find((b) => b.textContent?.includes('Not now'))!.click();

    expect(store.selectSignal(wishlistFeature.selectPromptOpen)()).toBe(false);
  });
});
