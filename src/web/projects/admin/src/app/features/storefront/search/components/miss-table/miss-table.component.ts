import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MissRow } from '../../models';

/** Searches that found nothing, most frequent first, so staff can add the missing synonym or product (SRC-13). */
@Component({
  selector: 'app-miss-table',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  styles: ':host { display: block; }',
  template: `
    @if (loading()) {
      <p class="state-message">Loading…</p>
    } @else if (misses().length === 0) {
      <p class="state-message">No searches have come back empty yet.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Searches that found nothing</caption>
        <thead>
          <tr>
            <th scope="col">Search text</th>
            <th scope="col">Day</th>
            <th scope="col">Times</th>
          </tr>
        </thead>
        <tbody>
          @for (row of misses(); track row.text + row.day) {
            <tr>
              <th scope="row" data-label="Search text">{{ row.text }}</th>
              <td data-label="Day">{{ row.day | date: 'd MMM y' }}</td>
              <td data-label="Times">{{ row.count }}</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
})
export class MissTableComponent {
  readonly misses = input.required<readonly MissRow[]>();
  readonly loading = input.required<boolean>();
}
