import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuditEntryDto } from '../../models';

/** The screens that show one record of a table. Tables without an entry here simply have no link. */
const RECORD_SCREENS: Readonly<Record<string, string>> = { USR_MAIN: '/staff', STYL_MAIN: '/styles' };

/** One entry: who, when, which record and the names of the columns that changed. It has no values to show, by design (AC-6). */
@Component({
  selector: 'app-audit-detail',
  imports: [DatePipe, RouterLink],
  template: `
    <article class="card" aria-labelledby="ad-heading">
      <h2 id="ad-heading">Entry {{ entry().id }}</h2>
      <dl>
        <dt>When</dt>
        <dd>
          <time [attr.datetime]="entry().occurredAt">{{ entry().occurredAt | date: 'medium' }}</time>
          <span class="field-hint">({{ entry().occurredAt }})</span>
        </dd>
        <dt>Who</dt>
        <dd>{{ entry().actor }}</dd>
        <dt>What happened</dt>
        <dd>{{ entry().operation }}</dd>
        <dt>Record</dt>
        <dd>
          <code>{{ entry().entityType }}</code> · {{ entry().entityKey }} ({{ entry().module }})
          @if (screen(); as path) {
            <a [routerLink]="path">Open the screen</a>
          }
        </dd>
        <dt>Request</dt>
        <dd>{{ entry().correlationId ?? 'None' }}</dd>
        <dt>Changed columns</dt>
        <dd>
          @if (entry().columns.length === 0) {
            None named (a secret field changed, or only bookkeeping did).
          } @else {
            <ul>
              @for (column of entry().columns; track column) {
                <li>
                  <code>{{ column }}</code>
                </li>
              }
            </ul>
          }
        </dd>
      </dl>
      <p class="field-hint">The trail records which columns changed, never their values.</p>
      <div class="form-actions">
        <button type="button" class="button" (click)="closed.emit()">Back to the list</button>
      </div>
    </article>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditDetailComponent {
  readonly entry = input.required<AuditEntryDto>();

  readonly closed = output<void>();

  readonly screen = computed(() => (this.entry().operation === 'DELETE' ? null : (RECORD_SCREENS[this.entry().entityType] ?? null)));
}
