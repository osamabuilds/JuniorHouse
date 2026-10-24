import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FieldErrorsComponent, inputValue, selectValue } from '@shared';
import { TARGET_KIND_OPTIONS, TARGET_REFERENCE_HINTS } from '../../storefront-settings.constants';
import { LinkTargetKind, Target } from '../../models';

/**
 * Where a link goes: a kind and a reference, never a free web address (the server checks both). The two controls are plain, labelled
 * and keyboard-operable (ST-15).
 */
@Component({
  selector: 'app-link-target-picker',
  imports: [FieldErrorsComponent],
  template: `
    <div class="form-field">
      <label [for]="idPrefix() + '-kind'">{{ label() }} goes to</label>
      <select [id]="idPrefix() + '-kind'" (change)="kindChanged($event)">
        @for (option of kinds(); track option.value) {
          <option [value]="option.value" [selected]="option.value === current().kind">{{ option.label }}</option>
        }
      </select>
    </div>
    <div class="form-field">
      <label [for]="idPrefix() + '-reference'">{{ referenceLabel() }}</label>
      <input
        [id]="idPrefix() + '-reference'"
        type="text"
        maxlength="300"
        autocomplete="off"
        [value]="current().reference"
        [attr.aria-describedby]="idPrefix() + '-hint'"
        (input)="referenceChanged($event)"
      />
      <small class="field-hint" [id]="idPrefix() + '-hint'">{{ hint() }}</small>
      <app-field-errors [messages]="errors()" />
    </div>
  `,
  styles: ':host { display: contents; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class LinkTargetPickerComponent {
  readonly target = input<Target | null>(null);
  readonly idPrefix = input.required<string>();
  /** What the link is called in the labels, for example "Footer link" or "Entry". */
  readonly label = input('Link');
  readonly errors = input<readonly string[]>([]);

  readonly changed = output<Target>();

  readonly current = computed<Target>(() => this.target() ?? { kind: 'Path', reference: '' });

  /** A kind that is not offered any more (a category link) stays visible so it is not silently replaced. */
  readonly kinds = computed(() => {
    const kind = this.current().kind;
    return TARGET_KIND_OPTIONS.some((o) => o.value === kind) ? TARGET_KIND_OPTIONS : [...TARGET_KIND_OPTIONS, { value: kind, label: kind }];
  });

  readonly hint = computed(() => TARGET_REFERENCE_HINTS[this.current().kind]);
  readonly referenceLabel = computed(() => {
    const labels: Record<LinkTargetKind, string> = {
      Path: 'Path',
      Page: 'Page address',
      Collection: 'Collection code',
      Category: 'Category code',
      External: 'Website address',
    };
    return labels[this.current().kind];
  });

  kindChanged(event: Event): void {
    this.changed.emit({ kind: selectValue(event) as LinkTargetKind, reference: this.current().reference });
  }

  referenceChanged(event: Event): void {
    this.changed.emit({ kind: this.current().kind, reference: inputValue(event).trim() });
  }
}
