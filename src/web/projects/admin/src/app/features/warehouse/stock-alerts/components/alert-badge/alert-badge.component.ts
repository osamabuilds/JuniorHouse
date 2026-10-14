import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of, switchMap, timer } from 'rxjs';
import { AlertApiService } from '../../services/alert-api.service';
import { BADGE_REFRESH_MS } from '../../stock-alerts.constants';
import { OpenAlertCountDto } from '../../models';

/**
 * The navigation badge (ALR-8): how many stock alerts are open, with the count in the text and in an accessible name, never colour
 * alone. It asks once on load and then every minute while the app is open; a failed request just keeps the last number. Shows nothing
 * when no alert is open.
 */
@Component({
  selector: 'app-alert-badge',
  template: `
    @if (count(); as c) {
      @if (c.open > 0) {
        <span class="badge badge--danger" role="status" [attr.aria-label]="label(c)">{{ c.open }}</span>
      }
    }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AlertBadgeComponent {
  private readonly api = inject(AlertApiService);

  readonly count = toSignal(
    timer(0, BADGE_REFRESH_MS).pipe(
      switchMap(() => this.api.openCount().pipe(catchError(() => of<OpenAlertCountDto | null>(null)))),
    ),
    { initialValue: null as OpenAlertCountDto | null },
  );

  label(count: OpenAlertCountDto): string {
    const open = `${count.open} open stock alert${count.open === 1 ? '' : 's'}`;
    return count.unacknowledged > 0 ? `${open}, ${count.unacknowledged} not yet seen` : open;
  }
}
