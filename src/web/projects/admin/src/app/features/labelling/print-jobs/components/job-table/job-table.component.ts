import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AppDatePipe } from '@shared';
import { PrintJobDto } from '../../models';
import { HasPermissionDirective } from '@core/auth';

/**
 * The print queue (PJ-13): one row per job with its state, attempts and last error, and the actions that apply. A Failed job can be
 * retried (the same job runs again, no new barcode); a Queued or Failed job can be cancelled. Presentational only.
 */
@Component({
  selector: 'app-job-table',
  imports: [HasPermissionDirective, AppDatePipe],
  template: `
    @if (loading() && jobs().length === 0) {
      <p class="state-message">Loading…</p>
    } @else if (jobs().length === 0) {
      <p class="state-message">No print jobs yet.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Print jobs</caption>
        <thead>
          <tr>
            <th scope="col">Job</th>
            <th scope="col">SKU</th>
            <th scope="col">Tags</th>
            <th scope="col">Printer</th>
            <th scope="col">Status</th>
            <th scope="col">Requested</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (job of jobs(); track job.id) {
            <tr>
              <td data-label="Job">
                <strong>{{ job.jobNo }}</strong><br />
                <small>{{ job.kindCode === 'Reprint' ? 'Reprint' : 'First printing' }}</small>
              </td>
              <td data-label="SKU">{{ job.skuCode }}<br /><small>{{ job.gtin }}</small></td>
              <td data-label="Tags">{{ job.qty }}</td>
              <td data-label="Printer">{{ job.printerCode }}</td>
              <td data-label="Status">
                <span class="badge" [class.badge--muted]="job.statusCode === 'Cancelled'">{{ statusText(job) }}</span>
                @if (job.statusCode === 'Queued' && job.attemptCount > 0) {
                  <br /><small>Attempt {{ job.attemptCount }} failed; trying again {{ job.nextAttemptAt | appDate: 'datetime' }}.</small>
                }
                @if (job.lastError && job.statusCode !== 'Printed') {
                  <br /><small class="field-error">{{ job.lastError }}</small>
                }
                @if (job.reprintReasonCode) {
                  <br /><small>Reason: {{ job.reprintReasonCode }}{{ job.note ? ' (' + job.note + ')' : '' }}</small>
                }
              </td>
              <td data-label="Requested">
                {{ job.requestedBy }}<br /><small>{{ job.createdAt | appDate: 'datetime' }}</small>
                @if (job.manualActionBy) {
                  <br /><small>Last action by {{ job.manualActionBy }}</small>
                }
              </td>
              <td data-label="Actions">
                @if (job.statusCode === 'Failed') {
                  <button *hasPermission="'labelling.write'" type="button" class="button button--small" [disabled]="busyId() === job.id" (click)="retry.emit(job)">Retry</button>
                }
                @if (job.statusCode === 'Queued' || job.statusCode === 'Failed') {
                  <button *hasPermission="'ops.admin'" type="button" class="button button--small button--danger" [disabled]="busyId() === job.id" (click)="cancel.emit(job)">Cancel</button>
                }
              </td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: ':host { display: block; } small { color: var(--color-muted, #555); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class JobTableComponent {
  readonly jobs = input.required<readonly PrintJobDto[]>();
  readonly loading = input.required<boolean>();
  readonly busyId = input<number | null>(null);

  readonly retry = output<PrintJobDto>();
  readonly cancel = output<PrintJobDto>();

  statusText(job: PrintJobDto): string {
    switch (job.statusCode) {
      case 'Queued':
        return 'Waiting for the printer';
      case 'Printing':
        return 'Printing now';
      case 'Printed':
        return 'Printed';
      case 'Failed':
        return 'Failed';
      default:
        return 'Cancelled';
    }
  }
}
