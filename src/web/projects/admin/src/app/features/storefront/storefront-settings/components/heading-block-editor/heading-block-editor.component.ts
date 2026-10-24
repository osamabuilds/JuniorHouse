import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { inputValue, selectValue } from '@shared';
import { PageBlock } from '../../models';

/** A section or sub-section heading. The first heading on a page must be a section heading (level 2); the server checks it (ST-17). */
@Component({
  selector: 'app-heading-block-editor',
  template: `
    <div class="form-grid">
      <div class="form-field">
        <label [for]="idPrefix() + '-level'">Heading size</label>
        <select [id]="idPrefix() + '-level'" (change)="levelChanged($event)">
          <option value="2" [selected]="block().level === 2">Section heading (level 2)</option>
          <option value="3" [selected]="block().level === 3">Sub-heading (level 3)</option>
        </select>
      </div>
      <div class="form-field">
        <label [for]="idPrefix() + '-text'">Heading text</label>
        <input [id]="idPrefix() + '-text'" type="text" maxlength="120" autocomplete="off" [value]="block().text" (input)="blockChange.emit({ ...block(), text: text($event) })" />
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class HeadingBlockEditorComponent {
  readonly block = input.required<PageBlock>();
  readonly idPrefix = input.required<string>();
  readonly blockChange = output<PageBlock>();

  levelChanged(event: Event): void {
    this.blockChange.emit({ ...this.block(), level: Number(selectValue(event)) });
  }

  text(event: Event): string {
    return inputValue(event);
  }
}
