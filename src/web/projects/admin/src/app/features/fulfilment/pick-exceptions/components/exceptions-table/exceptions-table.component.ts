import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { ExceptionRow, ResolveAction, reasonName } from '../../models';

export interface ResolveRequest {
  readonly id: number;
  readonly action: ResolveAction;
}

/** The exception queue (PEX-4): order, SKU, bin, system count, count found, picker and time. Presentational: it only reports a click. */
@Component({
  selector: 'app-exceptions-table',
  imports: [DatePipe],
  templateUrl: './exceptions-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExceptionsTableComponent {
  readonly rows = input.required<readonly ExceptionRow[]>();
  /** Whether the viewer may resolve (fulfilment.manage). The server checks it again on every call. */
  readonly canResolve = input(false);
  readonly resolvingId = input<number | null>(null);

  readonly resolve = output<ResolveRequest>();

  reason(code: string): string {
    return reasonName(code);
  }
}
