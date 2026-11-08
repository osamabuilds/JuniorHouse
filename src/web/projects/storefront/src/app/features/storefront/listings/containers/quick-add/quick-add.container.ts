import { ChangeDetectionStrategy, Component, OnDestroy, effect, inject, input, output, signal } from '@angular/core';
import { Store } from '@ngrx/store';
import { CartActions } from '../../../../cart/carts/store/cart.actions';
import { selectCartBusy, selectCartMessage } from '../../../../cart/carts/store/cart.selectors';
import { QuickAddBarComponent, QuickAddChoice, QuickAddState } from '../../components/quick-add-bar/quick-add-bar.component';
import { ListingTile } from '../../models/listing.model';

const CONFIRMATION_MS = 4000;

/**
 * Add to bag from a product card (CRD-5, 9, 10). Sends only the variant and a quantity of one through the cart's own add action, marked quiet
 * so the bag drawer stays shut and the bar confirms in place. Price and stock are the server's: a refusal comes back in words, the card
 * is asked to refresh from the server, and nothing is added.
 */
@Component({
  selector: 'rp-quick-add',
  imports: [QuickAddBarComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<rp-quick-add-bar
    [sizes]="tile().sizes"
    [productPath]="tile().path"
    [state]="state()"
    [busy]="busy()"
    [added]="added()"
    [notice]="notice()"
    (choose)="choose($event)"
    (close)="closed.emit()"
    (viewBag)="viewBag()"
  />`,
})
export class QuickAddContainer implements OnDestroy {
  private readonly store = inject(Store);

  readonly tile = input.required<ListingTile>();
  /** The visitor closed the bar. */
  readonly closed = output<void>();
  /** The server refused a size the card still showed as available: the page should load the listing again. */
  readonly stale = output<void>();
  /** The bag took the size: the add finished with no refusal. The page may count it (recommendation events, REC-12). */
  readonly bagged = output<void>();

  protected readonly busy = this.store.selectSignal(selectCartBusy);
  private readonly message = this.store.selectSignal(selectCartMessage);

  protected readonly state = signal<QuickAddState>('choosing');
  protected readonly added = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);

  // A signal, so that setting it runs the effect below even when the cart answers at once and busy ends where it began.
  private readonly pending = signal<QuickAddChoice | null>(null);
  private timer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    // The cart answers asynchronously: when the add has finished, a message means it was refused, none means it worked.
    effect(() => {
      const choice = this.pending();
      if (!choice || this.busy()) return;
      this.pending.set(null);
      const message = this.message();
      if (message) {
        this.notice.set(message);
        this.stale.emit();
        return;
      }
      const colour = this.tile().colourCount > 1 && choice.colourName ? `, ${choice.colourName}` : '';
      this.added.set(`Added, size ${choice.sizeName}${colour}`);
      this.state.set('added');
      this.bagged.emit();
      this.timer = setTimeout(() => this.state.set('choosing'), CONFIRMATION_MS);
    });
  }

  protected choose(choice: QuickAddChoice): void {
    clearTimeout(this.timer);
    this.notice.set(null);
    this.pending.set(choice);
    this.store.dispatch(CartActions.add({ variantId: choice.variantId, quantity: 1, quiet: true }));
  }

  protected viewBag(): void {
    this.store.dispatch(CartActions.openDrawer());
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
