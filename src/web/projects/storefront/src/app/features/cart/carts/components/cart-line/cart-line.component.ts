import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { CartLine } from '../../models/cart.model';
import { QuantityStepperComponent } from '../quantity-stepper/quantity-stepper.component';

/**
 * One basket line, in the page and in the drawer (design system: CartPage, CartDrawer). The server priced it; this only shows the figures
 * and says in words what needs attention: no longer available, short of stock, price changed (CRT-5, 7, 8). Never colour alone.
 */
@Component({
  selector: 'rp-cart-line',
  imports: [DecimalPipe, RouterLink, IconComponent, QuantityStepperComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @let l = line();
    <div class="rp-line" [class.rp-line--page]="!compact()">
      @if (l.imageUrl) {
        <picture class="rp-line__pic">
          <source type="image/webp" [attr.srcset]="l.imageUrl" />
          <img [src]="l.imageFallbackUrl || l.imageUrl" [alt]="l.imageAlt || l.name" loading="lazy" decoding="async" />
        </picture>
      } @else {
        <div class="rp-ph rp-ph--oat" role="img" [attr.aria-label]="l.imageAlt || l.name"></div>
      }
      <div class="rp-line__info">
        <div class="rp-line__head">
          @if (l.path) {
            <a class="rp-display-xs" [routerLink]="l.path" (click)="navigate.emit()">{{ l.name }}</a>
          } @else {
            <span class="rp-display-xs">{{ l.name }}</span>
          }
          @if (l.unitPrice !== null) {
            <p class="rp-lineprice">
              @if (l.compareAtPrice !== null && l.compareAtPrice > l.unitPrice) {
                <s><span class="sr-only">Was </span>Rs. {{ l.compareAtPrice * l.quantity | number }}</s>
              }
              Rs. {{ l.lineTotal | number }}
            </p>
          }
        </div>
        @if (l.colour || l.size) {
          <span class="rp-card__meta">{{ l.colour }}@if (l.colour && l.size) { · }@if (l.size) { Size {{ l.size }} }@if (l.unitPrice !== null && l.quantity > 1) { · Rs. {{ l.unitPrice | number }} each }</span>
        }
        @if (notes().length > 0) {
          <ul class="rp-bag-note">
            @for (note of notes(); track note) {
              <li>{{ note }}</li>
            }
          </ul>
        }
        <div class="rp-line__row">
          @if (isGone()) {
            <span></span>
          } @else {
            <rp-quantity-stepper [value]="l.quantity" [label]="l.name" [disabled]="busy()" (changed)="onQuantity($event)" />
          }
          <button type="button" class="rp-icon-btn rp-line__remove" [attr.aria-label]="'Remove ' + l.name" [disabled]="busy()" (click)="remove.emit()"><rp-icon name="trash" /></button>
        </div>
      </div>
    </div>
  `,
})
export class CartLineComponent {
  readonly line = input.required<CartLine>();
  /** The drawer shows a smaller line than the page. */
  readonly compact = input(false);
  /** A change is in flight: the controls wait. */
  readonly busy = input(false);
  readonly quantityChange = output<number>();
  readonly remove = output<void>();
  /** Following the item's link closes the drawer. */
  readonly navigate = output<void>();

  /** Minus at one removes the line (the server never accepts a quantity below one). */
  protected onQuantity(quantity: number): void {
    if (quantity < 1) this.remove.emit();
    else this.quantityChange.emit(quantity);
  }

  protected readonly isGone = computed(() => this.line().issues.includes('unavailable'));

  protected readonly notes = computed((): readonly string[] => {
    const l = this.line();
    const notes: string[] = [];
    if (l.issues.includes('unavailable')) notes.push('This item is no longer available. Remove it to continue.');
    if (l.issues.includes('low-stock')) {
      notes.push(l.available <= 0 ? 'Out of stock right now. Remove it or come back later.' : `Only ${l.available} in stock. Lower the quantity to continue.`);
    }
    if (l.issues.includes('price-changed') && l.previousPrice !== null && l.unitPrice !== null) {
      notes.push(`The price changed from Rs. ${Math.round(l.previousPrice).toLocaleString('en-PK')} to Rs. ${Math.round(l.unitPrice).toLocaleString('en-PK')} each.`);
    }
    return notes;
  });
}
