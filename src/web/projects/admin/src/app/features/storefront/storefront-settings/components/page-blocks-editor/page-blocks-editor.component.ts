import { ChangeDetectionStrategy, Component, input, output, signal } from '@angular/core';
import { selectValue } from '@shared';
import { BlockType, PageBlock } from '../../models';
import { BLOCK_TYPE_OPTIONS, MAX_BLOCKS_PER_PAGE } from '../../storefront-settings.constants';
import { blankBlock, changeBlockType } from '../../utils/blocks.util';
import { moveItem, removeItem, replaceItem } from '../../utils/list.util';
import { FactBlockEditorComponent } from '../fact-block-editor/fact-block-editor.component';
import { HeadingBlockEditorComponent } from '../heading-block-editor/heading-block-editor.component';
import { ListBlockEditorComponent } from '../list-block-editor/list-block-editor.component';
import { RowActionsComponent } from '../row-actions/row-actions.component';
import { TableBlockEditorComponent } from '../table-block-editor/table-block-editor.component';
import { TextBlockEditorComponent } from '../text-block-editor/text-block-editor.component';

/**
 * The body of a page as a list of blocks, each edited by the editor for its type. Order, change of type and removal are labelled
 * buttons and a select, never dragging (WCAG 2.5.7). Everything typed is plain text (ST-10).
 */
@Component({
  selector: 'app-page-blocks-editor',
  imports: [
    HeadingBlockEditorComponent,
    TextBlockEditorComponent,
    ListBlockEditorComponent,
    TableBlockEditorComponent,
    FactBlockEditorComponent,
    RowActionsComponent,
  ],
  template: `
    <ol class="blocks" aria-label="Page content">
      @for (block of blocks(); track $index; let i = $index; let first = $first; let last = $last) {
        <li class="card">
          <div class="form-field">
            <label [for]="'blk-type-' + i">Block {{ i + 1 }} is a</label>
            <select [id]="'blk-type-' + i" (change)="typeChanged(i, $event)">
              @for (option of types; track option.value) {
                <option [value]="option.value" [selected]="option.value === block.type">{{ option.label }}</option>
              }
            </select>
          </div>

          @switch (block.type) {
            @case ('Heading') {
              <app-heading-block-editor [block]="block" [idPrefix]="'blk-' + i" (blockChange)="edit(i, $event)" />
            }
            @case ('Paragraph') {
              <app-text-block-editor [block]="block" [idPrefix]="'blk-' + i" label="Paragraph" (blockChange)="edit(i, $event)" />
            }
            @case ('Callout') {
              <app-text-block-editor [block]="block" [idPrefix]="'blk-' + i" label="Highlighted note" (blockChange)="edit(i, $event)" />
            }
            @case ('List') {
              <app-list-block-editor [block]="block" [idPrefix]="'blk-' + i" (blockChange)="edit(i, $event)" />
            }
            @case ('Table') {
              <app-table-block-editor [block]="block" [idPrefix]="'blk-' + i" (blockChange)="edit(i, $event)" />
            }
            @case ('Fact') {
              <app-fact-block-editor [block]="block" [idPrefix]="'blk-' + i" (blockChange)="edit(i, $event)" />
            }
          }

          <app-row-actions [name]="'block ' + (i + 1) + ' (' + block.type + ')'" [first]="first" [last]="last" (up)="move(i, -1)" (down)="move(i, 1)" (remove)="remove(i)" />
        </li>
      } @empty {
        <li class="state-message">The page has no content yet. Add a block below.</li>
      }
    </ol>

    <div class="add-block">
      <div class="form-field">
        <label for="blk-new-type">Add a block</label>
        <select id="blk-new-type" (change)="newType.set(selectedType($event))">
          @for (option of types; track option.value) {
            <option [value]="option.value" [selected]="option.value === newType()">{{ option.label }}</option>
          }
        </select>
      </div>
      <button type="button" class="button" [disabled]="blocks().length >= max" (click)="add()">Add this block</button>
      @if (blocks().length >= max) {
        <p class="field-hint">A page can have at most {{ max }} blocks.</p>
      }
    </div>
  `,
  styles: `
    .blocks { list-style: none; margin: 0 0 var(--space-4, 1rem); padding: 0; }
    .blocks > li { margin-bottom: var(--space-3, 0.75rem); }
    .add-block { display: flex; flex-wrap: wrap; align-items: end; gap: var(--space-3, 0.75rem); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PageBlocksEditorComponent {
  readonly blocks = input.required<readonly PageBlock[]>();
  readonly blocksChange = output<readonly PageBlock[]>();

  readonly types = BLOCK_TYPE_OPTIONS;
  readonly max = MAX_BLOCKS_PER_PAGE;
  readonly newType = signal<BlockType>('Paragraph');

  edit(index: number, block: PageBlock): void {
    this.blocksChange.emit(replaceItem(this.blocks(), index, block));
  }

  typeChanged(index: number, event: Event): void {
    this.edit(index, changeBlockType(this.blocks()[index], selectValue(event) as BlockType));
  }

  move(index: number, delta: -1 | 1): void {
    this.blocksChange.emit(moveItem(this.blocks(), index, delta));
  }

  remove(index: number): void {
    this.blocksChange.emit(removeItem(this.blocks(), index));
  }

  add(): void {
    this.blocksChange.emit([...this.blocks(), blankBlock(this.newType())]);
  }

  selectedType(event: Event): BlockType {
    return selectValue(event) as BlockType;
  }
}
