import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { ApiError } from '@core/http';
import { LookupDto } from '@features/reference/lookups';
import { inputChecked, inputValue, selectNumberOrNull } from '@shared';
import { CHART_COLUMNS, ChartColumn, SizeChartDto, SizeChartFormValue, SizeChartLineInput } from '../../models';

interface Draft {
  included: boolean;
  values: Record<ChartColumn, string>;
}

const emptyValues = (): Record<ChartColumn, string> =>
  Object.fromEntries(CHART_COLUMNS.map(([field]) => [field, ''])) as Record<ChartColumn, string>;

/** "12" becomes 12, a blank becomes null; anything else is not a number. */
function parse(text: string): number | null | undefined {
  const trimmed = text.trim();
  if (trimmed === '') {
    return null;
  }

  const value = Number(trimmed);
  return Number.isFinite(value) ? value : undefined;
}

/**
 * The size chart editor (CHT-1, CHT-4, CHT-5, CHT-7): a real table with one row per size and a labelled input in every cell, so it works
 * by keyboard and for assistive technology. Ages are in months, lengths in cm, weight in kg. A row needs an age range or at least one
 * measurement; a minimum cannot be above its maximum. The same rules run in the browser first and again on the server.
 */
@Component({
  selector: 'app-size-chart-editor',
  template: `
    <form class="card" (submit)="$event.preventDefault(); submit()" aria-labelledby="sce-heading">
      <h2 id="sce-heading">{{ chart() ? 'Edit size chart' : 'New size chart' }}</h2>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }
      @if (problems().length > 0) {
        <div class="banner banner--error" role="alert">
          <p>This can't be saved yet:</p>
          <ul>
            @for (problem of problems(); track problem) {
              <li>{{ problem }}</li>
            }
          </ul>
        </div>
      }

      <div class="form-grid">
        <div class="form-field">
          <label for="sce-name">Chart name</label>
          <input id="sce-name" type="text" maxlength="100" [value]="name()" (input)="name.set(text($event))" />
        </div>
        <div class="form-field">
          <label for="sce-category">Category</label>
          <select id="sce-category" [disabled]="chart() !== null" (change)="categoryId.set(num($event))">
            <option value="">Choose a category</option>
            @for (category of categories(); track category.id) {
              <option [value]="category.id" [selected]="category.id === categoryId()">{{ category.name }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="sce-gender">For</label>
          <select id="sce-gender" [disabled]="chart() !== null" (change)="genderId.set(num($event))">
            <option value="">Choose</option>
            @for (gender of genders(); track gender.id) {
              <option [value]="gender.id" [selected]="gender.id === genderId()">{{ gender.name }}</option>
            }
          </select>
          <small class="field-hint">Unisex is the fallback for any audience without its own chart.</small>
        </div>
        <div class="form-field">
          <label for="sce-person">Your name</label>
          <input id="sce-person" type="text" maxlength="100" autocomplete="name" [value]="person()" (input)="person.set(text($event))" />
        </div>
      </div>

      <div class="table-scroll">
        <table>
          <caption>Rows of the chart: tick a size to include it</caption>
          <thead>
            <tr>
              <th scope="col">Size</th>
              @for (column of columns; track column[0]) {
                <th scope="col">{{ column[1] }}</th>
              }
            </tr>
          </thead>
          <tbody>
            @for (size of sizes(); track size.id) {
              <tr>
                <th scope="row">
                  <label>
                    <input type="checkbox" [checked]="draftOf(size.id).included" (change)="include(size.id, $event)" />
                    {{ size.name }}
                  </label>
                </th>
                @for (column of columns; track column[0]) {
                  <td>
                    <label>
                      <span class="sr-only">{{ column[1] }} for {{ size.name }}</span>
                      <input
                        type="text"
                        inputmode="decimal"
                        [disabled]="!draftOf(size.id).included"
                        [value]="draftOf(size.id).values[column[0]]"
                        (input)="setValue(size.id, column[0], $event)"
                      />
                    </label>
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save chart' }}</button>
        <button type="button" class="button" (click)="cancelled.emit()">Cancel</button>
        @if (chart()) {
          @if (confirmingDelete()) {
            <button type="button" class="button button--danger" (click)="confirmingDelete.set(false); deleted.emit(person())">Yes, delete this chart</button>
            <button type="button" class="button" (click)="confirmingDelete.set(false)">Keep it</button>
          } @else {
            <button type="button" class="button" (click)="confirmingDelete.set(true)">Delete</button>
          }
        }
      </div>
    </form>
  `,
  styles: ':host { display: block; margin-bottom: var(--space-5); } .table-scroll { position: relative; overflow-x: auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SizeChartEditorComponent {
  readonly chart = input<SizeChartDto | null>(null);
  readonly categories = input.required<readonly LookupDto[]>();
  readonly genders = input.required<readonly LookupDto[]>();
  readonly sizes = input.required<readonly LookupDto[]>();
  readonly saving = input(false);
  readonly error = input<ApiError | null>(null);
  readonly defaultPerson = input('');

  readonly submitted = output<SizeChartFormValue & { readonly person: string }>();
  readonly cancelled = output<void>();
  readonly deleted = output<string>();

  readonly columns = CHART_COLUMNS;
  readonly name = signal('');
  readonly categoryId = signal(0);
  readonly genderId = signal(0);
  readonly person = signal('');
  readonly problems = signal<readonly string[]>([]);
  readonly confirmingDelete = signal(false);
  private readonly drafts = signal<ReadonlyMap<number, Draft>>(new Map());

  readonly includedCount = computed(() => [...this.drafts().values()].filter((d) => d.included).length);

  constructor() {
    effect(() => {
      const chart = this.chart();
      untracked(() => {
        this.name.set(chart?.name ?? '');
        this.categoryId.set(chart?.categoryId ?? 0);
        this.genderId.set(chart?.genderId ?? 0);
        this.person.set(this.defaultPerson());
        this.problems.set([]);
        const next = new Map<number, Draft>();
        for (const line of chart?.lines ?? []) {
          const values = emptyValues();
          for (const [field] of CHART_COLUMNS) {
            const value = line[field];
            values[field] = value === null ? '' : String(value);
          }
          next.set(line.sizeId, { included: true, values });
        }
        this.drafts.set(next);
      });
    });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  num(event: Event): number {
    return selectNumberOrNull(event) ?? 0;
  }

  draftOf(sizeId: number): Draft {
    return this.drafts().get(sizeId) ?? { included: false, values: emptyValues() };
  }

  include(sizeId: number, event: Event): void {
    this.update(sizeId, { ...this.draftOf(sizeId), included: inputChecked(event) });
  }

  setValue(sizeId: number, field: ChartColumn, event: Event): void {
    const draft = this.draftOf(sizeId);
    this.update(sizeId, { ...draft, values: { ...draft.values, [field]: inputValue(event) } });
  }

  submit(): void {
    const problems: string[] = [];
    if (this.name().trim() === '') {
      problems.push('Give the chart a name.');
    }
    if (!this.chart() && (this.categoryId() === 0 || this.genderId() === 0)) {
      problems.push('Choose a category and who the chart is for.');
    }
    if (this.person().trim().length < 2) {
      problems.push('Type your name so the change can be recorded.');
    }

    const lines: SizeChartLineInput[] = [];
    for (const size of this.sizes()) {
      const draft = this.draftOf(size.id);
      if (!draft.included) {
        continue;
      }

      const parsed = {} as Record<ChartColumn, number | null>;
      let valid = true;
      for (const [field, label] of CHART_COLUMNS) {
        const value = parse(draft.values[field]);
        if (value === undefined || (value !== null && value < 0)) {
          problems.push(`${size.name}: ${label} must be a number, not negative.`);
          valid = false;
        } else {
          parsed[field] = value;
        }
      }

      if (!valid) {
        continue;
      }

      const rangeProblem =
        this.range(parsed.ageMinMonths, parsed.ageMaxMonths, 'age') ??
        this.range(parsed.heightMinCm, parsed.heightMaxCm, 'height') ??
        this.range(parsed.weightMinKg, parsed.weightMaxKg, 'weight');
      if (rangeProblem) {
        problems.push(`${size.name}: ${rangeProblem}`);
      } else if (Object.values(parsed).every((value) => value === null)) {
        problems.push(`${size.name}: give an age range or at least one measurement.`);
      } else {
        lines.push({ sizeId: size.id, ...parsed });
      }
    }

    if (lines.length === 0 && problems.length === 0) {
      problems.push('Tick at least one size.');
    }

    this.problems.set(problems);
    if (problems.length === 0) {
      this.submitted.emit({ categoryId: this.categoryId(), genderId: this.genderId(), name: this.name().trim(), lines, person: this.person().trim() });
    }
  }

  private range(min: number | null, max: number | null, what: string): string | null {
    return min !== null && max !== null && min > max ? `the lowest ${what} is above the highest.` : null;
  }

  private update(sizeId: number, draft: Draft): void {
    const next = new Map(this.drafts());
    next.set(sizeId, draft);
    this.drafts.set(next);
  }
}
