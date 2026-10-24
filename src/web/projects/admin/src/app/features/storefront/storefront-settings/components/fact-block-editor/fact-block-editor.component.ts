import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { inputValue, selectValue } from '@shared';
import { PageBlock } from '../../models';
import { FACT_OPTIONS } from '../../storefront-settings.constants';

/** A value taken from the settings (WhatsApp number, delivery table, ...). A page that quotes an empty one cannot be published (ST-17). */
@Component({
  selector: 'app-fact-block-editor',
  template: `
    <div class="form-grid">
      <div class="form-field">
        <label [for]="idPrefix() + '-fact'">Value to show</label>
        <select [id]="idPrefix() + '-fact'" (change)="blockChange.emit({ ...block(), factKey: select($event) })">
          @for (option of facts; track option.value) {
            <option [value]="option.value" [selected]="option.value === block().factKey">{{ option.label }}</option>
          }
        </select>
        <small class="field-hint">Filled in under Settings or Delivery and exchange.</small>
      </div>
      <div class="form-field">
        <label [for]="idPrefix() + '-label'">Heading above the value</label>
        <input [id]="idPrefix() + '-label'" type="text" maxlength="120" autocomplete="off" [value]="block().label ?? ''" (input)="blockChange.emit({ ...block(), label: text($event) || null })" />
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FactBlockEditorComponent {
  readonly block = input.required<PageBlock>();
  readonly idPrefix = input.required<string>();
  readonly blockChange = output<PageBlock>();

  readonly facts = FACT_OPTIONS;

  select(event: Event): string {
    return selectValue(event);
  }

  text(event: Event): string {
    return inputValue(event);
  }
}
