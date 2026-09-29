import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppDatePipe } from '@shared';
import { RunSummaryDto } from '../../models';
import { RUN_STATUS_LABELS } from '../../production.constants';
import { runFlags } from '../../utils';
import { RunFlagBadgesComponent } from '../run-flag-badges/run-flag-badges.component';

/** The production list (spec AC-16, AC-32, AC-33): one row per acknowledged PO, with its warnings as words. */
@Component({
  selector: 'app-run-table',
  imports: [RouterLink, AppDatePipe, RunFlagBadgesComponent],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (runs().length === 0) {
      <p class="state-message">
        No production runs yet. A run appears here as soon as a purchase order is acknowledged by the vendor.
      </p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">
          Production runs
        </caption>
        <thead>
          <tr>
            <th scope="col">Purchase order</th>
            <th scope="col">Run</th>
            <th scope="col">Sample</th>
            <th scope="col">Expected completion</th>
            <th scope="col">Shipped so far</th>
            <th scope="col">Warnings</th>
          </tr>
        </thead>
        <tbody>
          @for (run of runs(); track run.id) {
            <tr>
              <td data-label="Purchase order">
                <a [routerLink]="['/production', run.id]">{{ run.poNo }}</a>
              </td>
              <td data-label="Run">
                <span class="badge" [class.badge--muted]="run.statusId !== openStatusId">{{ statusLabels[run.statusId] }}</span>
              </td>
              <td data-label="Sample">
                {{ run.hasApprovedSample ? 'Approved' : 'Not approved yet' }}
              </td>
              <td data-label="Expected completion">
                @if (run.expectedCompletionDate) {
                  {{ run.expectedCompletionDate | appDate }}
                } @else {
                  {{ run.isCuttingStarted ? 'Not set' : 'Cutting not started' }}
                }
              </td>
              <td data-label="Shipped so far">{{ run.shippedQty }} pieces</td>
              <td data-label="Warnings">
                <app-run-flag-badges [flags]="flagsOf(run)" />
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RunTableComponent {
  readonly runs = input.required<readonly RunSummaryDto[]>();
  readonly loading = input.required<boolean>();

  readonly statusLabels = RUN_STATUS_LABELS;
  readonly openStatusId = 1;

  flagsOf(run: RunSummaryDto) {
    return runFlags(run);
  }
}
