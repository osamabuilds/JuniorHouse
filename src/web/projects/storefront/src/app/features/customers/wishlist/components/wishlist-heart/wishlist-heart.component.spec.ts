import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Store, provideStore } from '@ngrx/store';
import { WishlistActions } from '../../store/wishlist.actions';
import { wishlistFeature } from '../../store/wishlist.reducer';
import { WishlistHeartComponent } from './wishlist-heart.component';
import { provideState } from '@ngrx/store';

@Component({
  imports: [WishlistHeartComponent],
  template: `<a href="/x" (click)="followed = true"><button rpWishlistHeart [productId]="42" productName="Floral dress"></button></a>`,
})
class Host {
  followed = false;
}

describe('WishlistHeartComponent', () => {
  function setup() {
    TestBed.configureTestingModule({ providers: [provideStore({}), provideState(wishlistFeature)] });
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button') as HTMLButtonElement;
    return { fixture, button, store: TestBed.inject(Store) };
  }

  it('WSL-9 is a real button named for the product, drawn off until the saved ids are known', () => {
    const { button } = setup();

    expect(button.tagName).toBe('BUTTON');
    expect(button.getAttribute('type')).toBe('button');
    expect(button.getAttribute('aria-pressed')).toBe('false');
    expect(button.getAttribute('aria-label')).toBe('Add Floral dress to wishlist');
  });

  it('WSL-1 shows saved with aria-pressed and a remove name once the product is on the list', () => {
    const { fixture, button, store } = setup();

    store.dispatch(WishlistActions.sessionKnown({ signedIn: true }));
    store.dispatch(WishlistActions.idsLoaded({ result: { status: 'ok', ids: [42] } }));
    fixture.detectChanges();

    expect(button.getAttribute('aria-pressed')).toBe('true');
    expect(button.getAttribute('aria-label')).toBe('Remove Floral dress from wishlist');
  });

  it('WSL-1 pressing it toggles the product and never also follows a surrounding link', () => {
    const { fixture, button, store } = setup();
    const dispatch = vi.spyOn(store, 'dispatch');

    button.click();

    expect(dispatch).toHaveBeenCalledWith(WishlistActions.toggle({ productId: 42 }));
    expect(fixture.componentInstance.followed).toBe(false);
  });
});
