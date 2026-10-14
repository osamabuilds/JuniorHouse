import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { COUNT_STATUS_TEXT } from '../../cycle-counts.constants';
import { CycleCountRowDto } from '../../models';

/** The counts, newest first (CC-11): number, status in words, who started it, how many bins and lines, and a link to work on it. Presentational only. */
@Component({
  selector: 'app-count-list',
  imports: [DatePipe, RouterLink],
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (counts().length === 0) {
      <p class="state-message">No counts found.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Cycle counts</caption>
        <thead>
          <tr>
            <th scope="col">Count</th>
            <th scope="col">Status</th>
            <th scope="col">Started</th>
            <th scope="col">By</th>
            <th scope="col">Bins</th>
            <th scope="col">Lines counted</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (count of counts(); track count.id) {
            <tr>
              <td data-label="Count"><strong>{{ count.countNo }}</strong></td>
              <td data-label="Status">{{ statusText(count.statusCode) }}</td>
              <td data-label="Started">{{ count.createdAt | date: 'd MMM y, HH:mm' }}</td>
              <td data-label="By">{{ count.createdBy }}</td>
              <td data-label="Bins">{{ count.binCount }}</td>
              <td data-label="Lines counted">{{ count.lineCount }}</td>
              <td data-label="Actions">
                <a class="button button--small" [routerLink]="['/warehouse/cycle-counts', count.id]">
                  {{ count.statusCode === 'OPEN' ? 'Open the count' : 'View' }}
                </a>
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
export class CountListComponent {
  readonly counts = input.required<readonly CycleCountRowDto[]>();
  readonly loading = input.required<boolean>();

  statusText(code: string): string {
    return COUNT_STATUS_TEXT[code] ?? code;
  }
}
