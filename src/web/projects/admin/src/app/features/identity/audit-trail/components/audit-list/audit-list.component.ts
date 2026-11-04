import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AuditEntryDto } from '../../models';

const OPERATION_LABELS: Record<AuditEntryDto['operation'], string> = { INSERT: 'Created', UPDATE: 'Changed', DELETE: 'Deleted' };

/** The audit table, newest first. Presentational: the page owns the data and the paging. */
@Component({
  selector: 'app-audit-list',
  imports: [DatePipe],
  template: `
    @if (loading()) {
      <p class="state-message" role="status">Loading…</p>
    } @else if (entries().length === 0) {
      <p class="state-message">No entries match.</p>
    } @else {
      <table class="responsive">
        <caption class="sr-only">Audit entries, newest first</caption>
        <thead>
          <tr>
            <th scope="col">When</th>
            <th scope="col">Who</th>
            <th scope="col">What happened</th>
            <th scope="col">Record</th>
            <th scope="col">Actions</th>
          </tr>
        </thead>
        <tbody>
          @for (entry of entries(); track entry.id) {
            <tr>
              <td data-label="When">
                <time [attr.datetime]="entry.occurredAt" [title]="entry.occurredAt">{{ entry.occurredAt | date: 'medium' }}</time>
              </td>
              <td data-label="Who">{{ entry.actor }}</td>
              <td data-label="What happened">
                <span class="badge" [class.badge--muted]="entry.operation === 'DELETE'">{{ label(entry) }}</span>
              </td>
              <td data-label="Record">
                <code>{{ entry.entityType }}</code> · {{ entry.entityKey }} <span class="field-hint">({{ entry.module }})</span>
              </td>
              <td data-label="Actions">
                <button type="button" class="button button--small" [attr.aria-label]="'Open entry ' + entry.id" (click)="selected.emit(entry)">Open</button>
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
export class AuditListComponent {
  readonly entries = input.required<readonly AuditEntryDto[]>();
  readonly loading = input.required<boolean>();

  readonly selected = output<AuditEntryDto>();

  label(entry: AuditEntryDto): string {
    return OPERATION_LABELS[entry.operation];
  }
}
