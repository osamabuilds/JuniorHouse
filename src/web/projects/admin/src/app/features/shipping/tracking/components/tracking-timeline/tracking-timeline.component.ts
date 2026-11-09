import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TrackingEvent, sourceName } from '../../models';

/**
 * A shipment's full timeline for staff (TRK-13): every status the courier reported, oldest first, with when it happened and how we learned of it. A
 * failed attempt is marked in words, not by colour alone. Presentational.
 */
@Component({
  selector: 'app-tracking-timeline',
  imports: [DatePipe],
  templateUrl: './tracking-timeline.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TrackingTimelineComponent {
  readonly events = input.required<readonly TrackingEvent[]>();

  source(code: string): string {
    return sourceName(code);
  }

  isProblem(status: string): boolean {
    return status === 'FAILED_ATTEMPT' || status === 'RETURNED_TO_ORIGIN';
  }
}
