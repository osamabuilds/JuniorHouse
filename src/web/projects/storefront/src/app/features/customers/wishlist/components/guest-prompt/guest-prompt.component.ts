import { ChangeDetectionStrategy, Component, DestroyRef, effect, inject, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { RpDialog } from '../../../../../layout/dialog/dialog.directive';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { WishlistActions } from '../../store/wishlist.actions';
import { selectWishlistPromptOpen } from '../../store/wishlist.selectors';

/**
 * Shown when a signed-out shopper presses a heart (WSL-2, TC-PDP-07): the product is already remembered in this browser, and signing in or
 * registering keeps it (WSL-3). A native dialog, so focus is trapped, Esc closes it and focus returns to the heart.
 */
@Component({
  selector: 'rp-wishlist-guest-prompt',
  imports: [RouterLink, ButtonDirective, RpDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <dialog rpDialog class="rp-fullscreen rp-confirm" aria-labelledby="wishlist-prompt-title" (closed)="dismissed()">
      <div class="rp-dialog__head">
        <h2 id="wishlist-prompt-title" class="t-h3">Keep your wishlist</h2>
      </div>
      <div class="rp-dialog__body">
        <p>We have saved this on this device. Sign in or create an account to keep your wishlist everywhere.</p>
        <div class="wishlist-prompt__actions">
          <a rpBtn routerLink="/account/sign-in" [queryParams]="{ returnUrl: here() }" (click)="close()">Sign in</a>
          <a rpBtn="ghost" routerLink="/account/register" [queryParams]="{ returnUrl: here() }" (click)="close()">Create an account</a>
          <button rpBtn="ghost" type="button" (click)="close()">Not now</button>
        </div>
      </div>
    </dialog>
  `,
  styles: `
    .wishlist-prompt__actions { display: flex; flex-wrap: wrap; gap: var(--space-3); margin-top: var(--space-5); }
  `,
})
export class WishlistGuestPromptComponent {
  private readonly store = inject(Store);
  private readonly router = inject(Router);
  private readonly dialog = viewChild.required(RpDialog);
  private readonly open = this.store.selectSignal(selectWishlistPromptOpen);

  constructor() {
    effect(() => {
      if (this.open()) this.dialog().open();
    });
    inject(DestroyRef).onDestroy(() => this.dialog().close());
  }

  /** Where the shopper returns to after signing in: the page they were on. */
  protected here(): string {
    return this.router.url.split('#')[0];
  }

  protected close(): void {
    this.dialog().close();
  }

  protected dismissed(): void {
    this.store.dispatch(WishlistActions.closePrompt());
  }
}
