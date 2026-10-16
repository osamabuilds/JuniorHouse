import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { IconComponent } from '../icon/icon.component';

let nextId = 0;

/**
 * Label above a projected native control, with optional hint and error. Wire the control through the
 * template reference:
 * `<rp-field #f label="Mobile"><input class="rp-input" [id]="f.controlId" [attr.aria-describedby]="f.describedBy()" [attr.aria-invalid]="f.invalid()" /></rp-field>`
 */
@Component({
  selector: 'rp-field',
  imports: [IconComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-field">
      <label class="t-eyebrow" [for]="controlId">{{ label() }}</label>
      <ng-content />
      @if (hint()) {
        <p class="t-caption" [id]="hintId">{{ hint() }}</p>
      }
      @if (error()) {
        <p class="rp-error" [id]="errorId"><rp-icon name="alert" />{{ error() }}</p>
      }
    </div>
  `,
})
export class FieldComponent {
  private readonly uid = ++nextId;
  readonly label = input.required<string>();
  readonly hint = input('');
  /** A specific instruction that says what to do next. */
  readonly error = input('');

  readonly controlId = `rp-field-${this.uid}`;
  readonly hintId = `${this.controlId}-hint`;
  readonly errorId = `${this.controlId}-error`;

  readonly invalid = computed(() => (this.error() ? 'true' : null));
  readonly describedBy = computed(() => {
    const ids = [this.hint() ? this.hintId : '', this.error() ? this.errorId : ''].filter(Boolean);
    return ids.length ? ids.join(' ') : null;
  });
}
