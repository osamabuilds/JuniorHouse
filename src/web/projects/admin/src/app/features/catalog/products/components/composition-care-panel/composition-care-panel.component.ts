import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { LookupDto } from '@features/reference/lookups';
import { inputChecked, inputValue } from '@shared';
import { CompositionCareDto, CompositionRowInput } from '../../models';

interface Row {
  fibreId: number | null;
  percent: string;
}

/** The sum of the shares in integer tenths, so 33.3 + 33.3 + 33.4 is exactly 100 (CMP-7). */
export function totalPercent(rows: readonly Row[]): number {
  return rows.reduce((sum, row) => sum + Math.round((Number(row.percent) || 0) * 10), 0) / 10;
}

/**
 * The Composition and care tab (CMP-1..9): the fabric as rows of fibre and percentage that must total exactly 100%, the care
 * instructions to show (each with its text name), and a free-text note. The running total is announced politely so a screen reader
 * hears "Total 95%: 5% missing" as the person types.
 */
@Component({
  selector: 'app-composition-care-panel',
  template: `
    <form class="card" (submit)="$event.preventDefault(); saveComposition()" aria-labelledby="cmp-heading">
      <h3 id="cmp-heading">Fabric composition</h3>
      <table class="responsive">
        <caption class="sr-only">Fibres and their share of the fabric</caption>
        <thead>
          <tr>
            <th scope="col">Fibre</th>
            <th scope="col">Share (%)</th>
            <th scope="col"><span class="sr-only">Remove</span></th>
          </tr>
        </thead>
        <tbody>
          @for (row of rows(); track $index; let index = $index) {
            <tr>
              <td data-label="Fibre">
                <label>
                  <span class="sr-only">Fibre {{ index + 1 }}</span>
                  <select [disabled]="disabled()" (change)="setFibre(index, $event)">
                    <option value="" [selected]="row.fibreId === null">Choose a fibre</option>
                    @for (fibre of fibres(); track fibre.id) {
                      <option [value]="fibre.id" [selected]="row.fibreId === fibre.id">{{ fibre.name }}</option>
                    }
                  </select>
                </label>
              </td>
              <td data-label="Share (%)">
                <label>
                  <span class="sr-only">Share of fibre {{ index + 1 }} in percent</span>
                  <input type="text" inputmode="decimal" [disabled]="disabled()" [value]="row.percent" (input)="setPercent(index, $event)" />
                </label>
              </td>
              <td data-label="Remove">
                <button type="button" class="button button--small" [disabled]="disabled()" (click)="remove(index)">Remove</button>
              </td>
            </tr>
          }
        </tbody>
      </table>

      <p class="composition-total" [class.composition-total--ok]="total() === 100" role="status" aria-live="polite">{{ totalText() }}</p>
      @if (problem()) {
        <p class="field-error" role="alert">{{ problem() }}</p>
      }

      <div class="form-actions">
        <button type="button" class="button" [disabled]="disabled() || rows().length >= fibres().length" (click)="add()">Add a fibre</button>
        <button type="submit" class="button button--primary" [disabled]="disabled() || saving()">Save composition</button>
      </div>
    </form>

    <form class="card" (submit)="$event.preventDefault(); saveCare()" aria-labelledby="care-heading">
      <h3 id="care-heading">Care instructions</h3>
      <fieldset [disabled]="disabled()">
        <legend class="sr-only">Care instructions</legend>
        @for (item of careOptions(); track item.id) {
          <label class="checkbox">
            <input type="checkbox" [checked]="careIds().has(item.id)" (change)="toggleCare(item.id, $event)" />
            {{ item.name }}
          </label>
        }
      </fieldset>
      <div class="form-field">
        <label for="care-note">Care note (optional)</label>
        <textarea id="care-note" rows="2" maxlength="500" [disabled]="disabled()" [value]="note()" (input)="note.set(text($event))"></textarea>
        <small class="field-hint">Plain text, up to 500 characters.</small>
      </div>
      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="disabled() || saving()">Save care</button>
      </div>
    </form>
  `,
  styles: `
    .composition-total { font-weight: 600; }
    .composition-total--ok { color: var(--color-success, #1b6e2d); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CompositionCarePanelComponent {
  readonly data = input.required<CompositionCareDto | null>();
  readonly fibres = input.required<readonly LookupDto[]>();
  readonly careOptions = input.required<readonly LookupDto[]>();
  readonly saving = input(false);
  readonly disabled = input(false);

  readonly compositionSaved = output<readonly CompositionRowInput[]>();
  readonly careSaved = output<{ readonly careIds: readonly number[]; readonly note: string | null }>();

  readonly rows = signal<Row[]>([]);
  readonly careIds = signal<ReadonlySet<number>>(new Set());
  readonly note = signal('');
  readonly problem = signal<string | null>(null);

  readonly total = computed(() => totalPercent(this.rows()));
  readonly totalText = computed(() => {
    const total = this.total();
    if (this.rows().length === 0) {
      return 'No composition yet. It must total 100%.';
    }
    if (total === 100) {
      return 'Total 100%';
    }
    return total < 100 ? `Total ${total}%: ${Math.round((100 - total) * 10) / 10}% missing` : `Total ${total}%: ${Math.round((total - 100) * 10) / 10}% too much`;
  });

  constructor() {
    effect(() => {
      const data = this.data();
      untracked(() => {
        this.rows.set((data?.rows ?? []).map((row) => ({ fibreId: row.fibreId, percent: String(row.percent) })));
        this.careIds.set(new Set((data?.care ?? []).map((care) => care.id)));
        this.note.set(data?.careNote ?? '');
        this.problem.set(null);
      });
    });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  add(): void {
    this.rows.update((rows) => [...rows, { fibreId: null, percent: '' }]);
  }

  remove(index: number): void {
    this.rows.update((rows) => rows.filter((_, position) => position !== index));
  }

  setFibre(index: number, event: Event): void {
    const value = inputValue(event);
    this.rows.update((rows) => rows.map((row, position) => (position === index ? { ...row, fibreId: value ? +value : null } : row)));
  }

  setPercent(index: number, event: Event): void {
    const value = inputValue(event);
    this.rows.update((rows) => rows.map((row, position) => (position === index ? { ...row, percent: value } : row)));
  }

  toggleCare(id: number, event: Event): void {
    const next = new Set(this.careIds());
    if (inputChecked(event)) {
      next.add(id);
    } else {
      next.delete(id);
    }
    this.careIds.set(next);
  }

  saveComposition(): void {
    const rows = this.rows();
    const fibreIds = rows.map((row) => row.fibreId);
    if (rows.some((row) => row.fibreId === null)) {
      this.problem.set('Choose a fibre for every row.');
      return;
    }
    if (new Set(fibreIds).size !== fibreIds.length) {
      this.problem.set('Each fibre can be listed only once.');
      return;
    }
    if (rows.some((row) => !(Number(row.percent) > 0) || Number(row.percent) > 100 || Math.round(Number(row.percent) * 10) / 10 !== Number(row.percent))) {
      this.problem.set('Each share must be more than 0 and at most 100, with at most one decimal place.');
      return;
    }

    this.problem.set(null);
    this.compositionSaved.emit(rows.map((row) => ({ fibreId: row.fibreId!, percent: Number(row.percent) })));
  }

  saveCare(): void {
    this.careSaved.emit({ careIds: [...this.careIds()], note: this.note().trim() || null });
  }
}
