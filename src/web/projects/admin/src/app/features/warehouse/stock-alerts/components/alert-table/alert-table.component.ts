import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AlertRowDto } from '../../models';

/**
 * Open low-stock and stock-out alerts (ALR-8). The level is shown in words, not colour alone. Acknowledging records who has seen the alert;
 * it stays open until stock recovers (ALR-5). Presentational only.
 */
@Component({
  selector: 'app-alert-table',
  imports: [DatePipe],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (alerts().length === 0) {
      <p class="state-message">No open alerts. Everything is above its threshold.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Open stock alerts</caption>
        <thead>
          <tr>
            <th scope="col">SKU</th>
            <th scope="col">Alert</th>
            <th scope="col">Available</th>
            <th scope="col">Threshold</th>
            <th scope="col">Since</th>
            <th scope="col">Seen by</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (alert of alerts(); track alert.id) {
            <tr>
              <td data-label="SKU"><strong>{{ alert.skuCode }}</strong><br /><small>{{ alert.gtin }}</small></td>
              <td data-label="Alert">
                @if (alert.levelCode === 'OUT') {
                  <span class="badge badge--danger">Stock-out</span>
                } @else {
                  <span class="badge">Low stock</span>
                }
              </td>
              <td data-label="Available">{{ alert.available }}</td>
              <td data-label="Threshold">{{ alert.threshold }}</td>
              <td data-label="Since">{{ alert.openedAt | date: 'd MMM y, HH:mm' }}</td>
              <td data-label="Seen by">
                @if (alert.acknowledged) {
                  {{ alert.acknowledgedBy }}
                } @else {
                  <span class="badge badge--muted">Not yet</span>
                }
              </td>
              <td data-label="Actions">
                <button
                  type="button"
                  class="button button--small"
                  [disabled]="alert.acknowledged || acknowledgingId() === alert.id"
                  (click)="acknowledge.emit(alert)"
                >
                  {{ alert.acknowledged ? 'Acknowledged' : 'Acknowledge' }}
                </button>
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
export class AlertTableComponent {
  readonly alerts = input.required<readonly AlertRowDto[]>();
  readonly loading = input.required<boolean>();
  readonly acknowledgingId = input<number | null>(null);

  readonly acknowledge = output<AlertRowDto>();
}
