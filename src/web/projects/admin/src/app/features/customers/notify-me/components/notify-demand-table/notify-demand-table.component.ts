import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { NotifyDemandRow } from '../../models';

/** The demand list itself (NTM-11): one row per size with people waiting. Counts and dates only, never an address (NTM-12). Presentational. */
@Component({
  selector: 'app-notify-demand-table',
  imports: [DatePipe],
  templateUrl: './notify-demand-table.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NotifyDemandTableComponent {
  readonly rows = input.required<readonly NotifyDemandRow[]>();
}
