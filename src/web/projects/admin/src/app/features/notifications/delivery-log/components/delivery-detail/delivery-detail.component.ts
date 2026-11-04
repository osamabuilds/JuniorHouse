import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { HasPermissionDirective } from '@core/auth';
import { CHANNEL_CHOICES, DeliveryDetail, ERROR_CHOICES, KIND_CHOICES, STATUS_CHOICES } from '../../models';
import { nameOf, newResendKey } from '../../utils/delivery-labels.util';

/** What the resend button hands up: the delivery, the key that makes a double click harmless (DLG-8), and the yes to a duplicate warning (DLG-7). */
export interface ResendRequest {
  readonly id: number;
  readonly key: string;
  readonly acknowledgeDuplicate: boolean;
}

/**
 * One message in full (DLG-1, DLV-12): where it stands, every attempt with its error category, and the Resend action. Resend asks first; when the
 * message was probably received, the question says so (DLG-7). The key is made when the question opens and reused until it closes, so pressing
 * the confirm button twice sends one resend. The server checks `notifications.resend` again on every request (SEC-08).
 */
@Component({
  selector: 'app-delivery-detail',
  imports: [DatePipe, HasPermissionDirective],
  templateUrl: './delivery-detail.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryDetailComponent {
  readonly detail = input.required<DeliveryDetail>();
  readonly resending = input(false);

  readonly resendRequested = output<ResendRequest>();
  readonly closed = output<void>();

  readonly confirming = signal(false);
  private key = '';

  kind(code: string): string {
    return nameOf(KIND_CHOICES, code);
  }

  channel(code: string): string {
    return nameOf(CHANNEL_CHOICES, code);
  }

  status(code: string): string {
    return nameOf(STATUS_CHOICES, code);
  }

  error(code: string | null): string {
    return nameOf(ERROR_CHOICES, code);
  }

  askResend(): void {
    this.key = newResendKey();
    this.confirming.set(true);
  }

  keep(): void {
    this.confirming.set(false);
  }

  confirm(): void {
    const detail = this.detail();
    this.resendRequested.emit({ id: detail.row.id, key: this.key, acknowledgeDuplicate: detail.warnsOfDuplicate });
    this.confirming.set(false);
  }
}
