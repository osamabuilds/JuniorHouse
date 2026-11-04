import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, untracked, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { RpDialog } from '../../../../../layout/dialog/dialog.directive';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { CartLineComponent } from '../../components/cart-line/cart-line.component';
import { CartActions } from '../../store/cart.actions';
import { selectCart, selectCartBusy, selectCartDrawerOpen, selectCartIsEmpty, selectCartMessage } from '../../store/cart.selectors';

/**
 * The bag drawer (design system: CartDrawer): a right-hand dialog opened by the header's bag button and after Add to bag. Native `<dialog>`
 * through `rpDialog`, so focus is trapped, Esc closes it and focus returns to whatever opened it. It shows the same server-priced lines
 * as the page, smaller, and a link to the full bag.
 */
@Component({
  selector: 'rp-mini-cart',
  imports: [DecimalPipe, RouterLink, RpDialog, ButtonDirective, IconComponent, CartLineComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mini-cart.container.html',
})
export class MiniCartContainer {
  private readonly store = inject(Store);
  private readonly dialog = viewChild(RpDialog);

  protected readonly cart = this.store.selectSignal(selectCart);
  protected readonly empty = this.store.selectSignal(selectCartIsEmpty);
  protected readonly busy = this.store.selectSignal(selectCartBusy);
  protected readonly message = this.store.selectSignal(selectCartMessage);
  private readonly open = this.store.selectSignal(selectCartDrawerOpen);

  constructor() {
    // The store decides whether the drawer is open; the dialog follows.
    effect(() => {
      const wanted = this.open();
      const dialog = this.dialog();
      if (!dialog) return;
      untracked(() => (wanted ? dialog.open() : dialog.close()));
    });
  }

  protected close(): void {
    this.store.dispatch(CartActions.closeDrawer());
  }

  protected setQuantity(variantId: number, quantity: number): void {
    this.store.dispatch(CartActions.setQuantity({ variantId, quantity }));
  }

  protected remove(variantId: number): void {
    this.store.dispatch(CartActions.remove({ variantId }));
  }
}
