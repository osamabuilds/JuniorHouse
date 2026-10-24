import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { inputChecked, inputValue } from '@shared';
import { PageBlock } from '../../models';
import { listItems, withListItems } from '../../utils/blocks.util';
import { moveItem, removeItem } from '../../utils/list.util';
import { RowActionsComponent } from '../row-actions/row-actions.component';

/** A bulleted or numbered list, one line per item. */
@Component({
  selector: 'app-list-block-editor',
  imports: [RowActionsComponent],
  template: `
    <label class="check">
      <input type="checkbox" [checked]="block().ordered" (change)="blockChange.emit({ ...block(), ordered: checked($event) })" />
      Numbered (for steps) instead of bulleted
    </label>
    <ul class="rows" aria-label="List items">
      @for (item of lines(); track $index; let i = $index; let first = $first; let last = $last) {
        <li>
          <div class="form-field">
            <label [for]="idPrefix() + '-item-' + i">Item {{ i + 1 }}</label>
            <input [id]="idPrefix() + '-item-' + i" type="text" maxlength="500" autocomplete="off" [value]="item" (input)="edit(i, text($event))" />
          </div>
          <app-row-actions [name]="'list item ' + (i + 1)" [first]="first" [last]="last" (up)="move(i, -1)" (down)="move(i, 1)" (remove)="remove(i)" />
        </li>
      }
    </ul>
    <button type="button" class="button button--small" (click)="add()">Add an item</button>
  `,
  styles: '.rows { list-style: none; margin: var(--space-3, 0.75rem) 0; padding: 0; } li { margin-bottom: var(--space-3, 0.75rem); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ListBlockEditorComponent {
  readonly block = input.required<PageBlock>();
  readonly idPrefix = input.required<string>();
  readonly blockChange = output<PageBlock>();

  readonly lines = computed(() => listItems(this.block()));

  edit(index: number, text: string): void {
    this.blockChange.emit(withListItems(this.block(), this.lines().map((l, i) => (i === index ? text : l))));
  }

  move(index: number, delta: -1 | 1): void {
    this.blockChange.emit(withListItems(this.block(), moveItem(this.lines(), index, delta)));
  }

  remove(index: number): void {
    this.blockChange.emit(withListItems(this.block(), removeItem(this.lines(), index)));
  }

  add(): void {
    this.blockChange.emit(withListItems(this.block(), [...this.lines(), '']));
  }

  text(event: Event): string {
    return inputValue(event);
  }

  checked(event: Event): boolean {
    return inputChecked(event);
  }
}
