import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { AuditFormValue, EMPTY_AUDIT_FORM } from '../../models';

/** The audit filter form. Presentational: it reports the values when the person applies them. */
@Component({
  selector: 'app-audit-filters',
  template: `
    <form class="card" aria-labelledby="af-heading" (submit)="apply($event)" (reset)="clear()">
      <h2 id="af-heading">Filter</h2>
      <div class="form-grid">
        <div class="form-field">
          <label for="af-actor">Person</label>
          <input id="af-actor" name="actor" type="text" maxlength="100" [value]="value().actor" />
        </div>
        <div class="form-field">
          <label for="af-module">Module</label>
          <input id="af-module" name="module" type="text" maxlength="20" [value]="value().module" placeholder="for example CTLG" />
        </div>
        <div class="form-field">
          <label for="af-type">Record type</label>
          <input id="af-type" name="entityType" type="text" maxlength="100" [value]="value().entityType" placeholder="for example STYL_MAIN" />
        </div>
        <div class="form-field">
          <label for="af-key">Record key</label>
          <input id="af-key" name="entityKey" type="text" maxlength="200" [value]="value().entityKey" />
        </div>
        <div class="form-field">
          <label for="af-op">What happened</label>
          <select id="af-op" name="operation">
            <option value="" [selected]="value().operation === ''">Anything</option>
            <option value="INSERT" [selected]="value().operation === 'INSERT'">Created</option>
            <option value="UPDATE" [selected]="value().operation === 'UPDATE'">Changed</option>
            <option value="DELETE" [selected]="value().operation === 'DELETE'">Deleted</option>
          </select>
        </div>
        <div class="form-field">
          <label for="af-from">From</label>
          <input id="af-from" name="fromDate" type="date" [value]="value().fromDate" />
        </div>
        <div class="form-field">
          <label for="af-to">To</label>
          <input id="af-to" name="toDate" type="date" [value]="value().toDate" />
        </div>
      </div>
      <p class="field-hint">Without dates the list covers the last 30 days; a range can span at most 366 days.</p>
      <div class="form-actions">
        <button type="submit" class="button button--primary">Apply filters</button>
        <button type="reset" class="button">Clear</button>
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AuditFiltersComponent {
  readonly value = input<AuditFormValue>(EMPTY_AUDIT_FORM);

  readonly applied = output<AuditFormValue>();

  apply(event: Event): void {
    event.preventDefault();
    const data = new FormData(event.target as HTMLFormElement);
    const read = (name: string): string => ((data.get(name) as string | null) ?? '').trim();
    this.applied.emit({
      actor: read('actor'),
      module: read('module').toUpperCase(),
      entityType: read('entityType').toUpperCase(),
      entityKey: read('entityKey'),
      operation: read('operation'),
      fromDate: read('fromDate'),
      toDate: read('toDate'),
    });
  }

  clear(): void {
    this.applied.emit(EMPTY_AUDIT_FORM);
  }
}
