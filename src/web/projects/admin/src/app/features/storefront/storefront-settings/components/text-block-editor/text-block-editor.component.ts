import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { inputValue } from '@shared';
import { PageBlock } from '../../models';
import { MAX_PARAGRAPH_LENGTH } from '../../storefront-settings.constants';

/** A paragraph or a highlighted note. Plain text only: markup typed here is shown as text and never run (ST-10). */
@Component({
  selector: 'app-text-block-editor',
  template: `
    <div class="form-field form-field--wide">
      <label [for]="idPrefix() + '-text'">{{ label() }}</label>
      <textarea [id]="idPrefix() + '-text'" rows="4" [attr.maxlength]="max" [attr.aria-describedby]="idPrefix() + '-count'" [value]="block().text" (input)="blockChange.emit({ ...block(), text: text($event) })"></textarea>
      <small class="field-hint" [id]="idPrefix() + '-count'">Plain text. {{ remaining() }} characters left.</small>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TextBlockEditorComponent {
  readonly block = input.required<PageBlock>();
  readonly idPrefix = input.required<string>();
  readonly label = input('Text');
  readonly blockChange = output<PageBlock>();

  readonly max = MAX_PARAGRAPH_LENGTH;
  readonly remaining = computed(() => this.max - this.block().text.length);

  text(event: Event): string {
    return inputValue(event);
  }
}
