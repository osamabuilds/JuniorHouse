import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FieldErrorsComponent, inputChecked, inputValue } from '@shared';
import { AgeStep } from '../../models';
import { canMove, moveItem, removeItem, replaceItem, withSort } from '../../utils/list.util';
import { RowActionsComponent } from '../row-actions/row-actions.component';

/**
 * The "Shop by age" list (S-2, ST-2). A code that already exists is read-only: it is the key in links, so renaming a label never
 * changes a URL. New rows get a code to type. Order, show/hide and removal are buttons and a checkbox.
 */
@Component({
  selector: 'app-ages-editor',
  imports: [FieldErrorsComponent, RowActionsComponent],
  template: `
    <ul class="rows" aria-label="Ages">
      @for (step of steps(); track $index; let i = $index; let first = $first; let last = $last) {
        <li class="card">
          <div class="form-grid">
            <div class="form-field">
              <label [for]="'age-code-' + i">Code</label>
              <input
                [id]="'age-code-' + i"
                type="text"
                maxlength="20"
                autocomplete="off"
                [value]="step.code"
                [readOnly]="isLocked(step.code)"
                [attr.aria-describedby]="'age-code-hint-' + i"
                (input)="edit(i, { code: text($event).trim().toLowerCase() })"
              />
              <small class="field-hint" [id]="'age-code-hint-' + i">
                {{ isLocked(step.code) ? 'Used in links, so it cannot change.' : 'Lowercase letters, digits and hyphens, for example 3y.' }}
              </small>
            </div>
            <div class="form-field">
              <label [for]="'age-label-' + i">Label</label>
              <input [id]="'age-label-' + i" type="text" maxlength="40" required autocomplete="off" [value]="step.label" (input)="edit(i, { label: text($event) })" />
              <small class="field-hint">What shoppers read, for example 3Y.</small>
            </div>
            <div class="form-field">
              <label class="check">
                <input type="checkbox" [checked]="step.enabled" (change)="edit(i, { enabled: checked($event) })" />
                Shown on the shop
              </label>
            </div>
          </div>
          <app-field-errors [messages]="errors()['ages.' + step.code] ?? []" />
          <app-row-actions
            [name]="'the ' + (step.label || step.code || 'new') + ' age'"
            [first]="!move(i, -1)"
            [last]="!move(i, 1)"
            [removable]="!isLocked(step.code)"
            (up)="reorder(i, -1)"
            (down)="reorder(i, 1)"
            (remove)="remove(i)"
          />
          @if (isLocked(step.code)) {
            <p class="field-hint">A published age cannot be removed. Switch it off instead.</p>
          }
        </li>
      } @empty {
        <li class="state-message">There are no ages yet.</li>
      }
    </ul>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgesEditorComponent {
  readonly steps = input.required<readonly AgeStep[]>();
  /** Codes that exist in the saved draft; they cannot be renamed. */
  readonly lockedCodes = input<readonly string[]>([]);
  readonly errors = input<Readonly<Record<string, readonly string[]>>>({});

  readonly stepsChange = output<readonly AgeStep[]>();

  isLocked(code: string): boolean {
    return this.lockedCodes().includes(code);
  }

  move(index: number, delta: -1 | 1): boolean {
    return canMove(this.steps(), index, delta);
  }

  edit(index: number, patch: Partial<AgeStep>): void {
    this.stepsChange.emit(replaceItem(this.steps(), index, patch));
  }

  reorder(index: number, delta: -1 | 1): void {
    this.stepsChange.emit(withSort(moveItem(this.steps(), index, delta)));
  }

  remove(index: number): void {
    this.stepsChange.emit(withSort(removeItem(this.steps(), index)));
  }

  text(event: Event): string {
    return inputValue(event);
  }

  checked(event: Event): boolean {
    return inputChecked(event);
  }
}
