import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { CHANNEL_CHOICES, DeliveryLogRow, ERROR_CHOICES, KIND_CHOICES, STATUS_CHOICES } from '../../models';
import { nameOf } from '../../utils/delivery-labels.util';

/** The log itself (DLG-1, DLG-6): one row per message per channel, recipients masked, no message body. Presentational: it only reports a click. */
@Component({
  selector: 'app-delivery-table',
  imports: [DatePipe],
  templateUrl: './delivery-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryTableComponent {
  readonly rows = input.required<readonly DeliveryLogRow[]>();
  readonly selectedId = input<number | null>(null);

  readonly selected = output<number>();

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

  isProblem(code: string): boolean {
    return code === 'FAIL' || code === 'DEAD';
  }
}
