import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { IconComponent } from '../../../../../shared/ui/icon/icon.component';
import { CodAnswer, CodLinkStatus, CodOutcome } from '../../models/cod.model';

/**
 * The two buttons and the words around them for a cash on delivery order (COD-12, COD-15, COD-19). Opening the link only shows this; a
 * press is the only thing that sends an answer. A link that is not valid, or an order that is already handled, gets a neutral message and no
 * buttons, so the page reveals nothing about other orders.
 */
@Component({
  selector: 'rp-cod-response',
  imports: [ButtonDirective, IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (loading()) {
      <p role="status">Checking your order…</p>
    } @else if (outcome() === 'Confirmed') {
      <div class="rp-notice" role="status">
        <rp-icon name="check" />
        <div><b>Thank you, your order is confirmed.</b><br />We will get it ready and message you when it ships. Please keep the cash ready for the courier.</div>
      </div>
    } @else if (outcome() === 'Cancelled') {
      <div class="rp-notice" role="status">
        <rp-icon name="check" />
        <div><b>Your order has been cancelled.</b><br />Nothing is owed. You are welcome back any time.</div>
      </div>
    } @else if (outcome() === 'AlreadyHandled' || (link() && link()!.isValid && !link()!.canRespond)) {
      <div class="rp-notice" role="status">
        <rp-icon name="alert" />
        <div><b>This order has already been dealt with.</b><br />There is nothing more to do here. If you need help, please contact us and quote your order number.</div>
      </div>
    } @else if (!link() || !link()!.isValid || outcome() === 'InvalidLink') {
      <div class="rp-notice rp-notice--error" role="alert">
        <rp-icon name="alert" />
        <div><b>We could not use this link.</b><br />It may have expired or been copied incompletely. If you need help, please contact us and quote your order number.</div>
      </div>
    } @else {
      <p class="rp-lead">Please confirm that you want this order{{ link()!.orderNumber ? ' (' + link()!.orderNumber + ')' : '' }} delivered, and that you will pay the courier in cash.</p>
      @if (failed()) {
        <div class="rp-notice rp-notice--error" role="alert">
          <rp-icon name="alert" />
          <div>We could not record your answer just now. Nothing has changed. Please try again.</div>
        </div>
      }
      <div class="rp-placed__actions">
        <button rpBtn="action" type="button" [disabled]="sending()" [attr.aria-busy]="sending()" (click)="answer.emit('confirm')">Confirm my order</button>
        <button rpBtn="ghost" type="button" [disabled]="sending()" (click)="answer.emit('cancel')">Cancel my order</button>
      </div>
    }
  `,
})
export class CodResponseComponent {
  readonly link = input<CodLinkStatus | null>(null);
  readonly loading = input(false);
  readonly sending = input(false);
  readonly failed = input(false);
  readonly outcome = input<CodOutcome | null>(null);

  readonly answer = output<CodAnswer>();

  protected readonly canRespond = computed(() => this.link()?.canRespond ?? false);
}
