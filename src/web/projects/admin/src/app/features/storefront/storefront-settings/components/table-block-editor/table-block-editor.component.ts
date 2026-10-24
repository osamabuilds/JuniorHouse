import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { inputValue } from '@shared';
import { PageBlock } from '../../models';
import { tableGrid, withGrid } from '../../utils/blocks.util';

/** A table: a caption, a header row and body rows of plain text. Rows and columns are added and removed with buttons. */
@Component({
  selector: 'app-table-block-editor',
  template: `
    <div class="form-field">
      <label [for]="idPrefix() + '-caption'">Table caption</label>
      <input [id]="idPrefix() + '-caption'" type="text" maxlength="120" autocomplete="off" [value]="block().label ?? ''" (input)="blockChange.emit({ ...block(), label: text($event) || null })" />
    </div>
    <table class="grid">
      <caption class="sr-only">Table cells</caption>
      <thead>
        <tr>
          @for (cell of grid()[0]; track $index; let c = $index) {
            <th scope="col">
              <label [for]="idPrefix() + '-h-' + c" class="sr-only">Header {{ c + 1 }}</label>
              <input [id]="idPrefix() + '-h-' + c" type="text" maxlength="60" [value]="cell" (input)="edit(0, c, text($event))" />
            </th>
          }
        </tr>
      </thead>
      <tbody>
        @for (row of bodyRows(); track $index; let r = $index) {
          <tr>
            @for (cell of row; track $index; let c = $index) {
              <td>
                <label [for]="idPrefix() + '-' + (r + 1) + '-' + c" class="sr-only">Row {{ r + 1 }}, column {{ c + 1 }}</label>
                <input [id]="idPrefix() + '-' + (r + 1) + '-' + c" type="text" maxlength="500" [value]="cell" (input)="edit(r + 1, c, text($event))" />
              </td>
            }
          </tr>
        }
      </tbody>
    </table>
    <div class="grid-actions">
      <button type="button" class="button button--small" (click)="addRow()">Add a row</button>
      <button type="button" class="button button--small" [disabled]="grid().length <= 1" (click)="removeRow()">Remove the last row</button>
      <button type="button" class="button button--small" (click)="addColumn()">Add a column</button>
      <button type="button" class="button button--small" [disabled]="grid()[0].length <= 1" (click)="removeColumn()">Remove the last column</button>
    </div>
  `,
  styles: `
    .grid { width: 100%; border-collapse: collapse; margin: var(--space-3, 0.75rem) 0; }
    .grid th, .grid td { padding: 0.25rem; }
    .grid input { width: 100%; min-width: 6rem; }
    .grid-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; }
    .sr-only { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
    :host { display: block; overflow-x: auto; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TableBlockEditorComponent {
  readonly block = input.required<PageBlock>();
  readonly idPrefix = input.required<string>();
  readonly blockChange = output<PageBlock>();

  readonly grid = computed(() => {
    const grid = tableGrid(this.block());
    return grid.length > 0 ? grid : [['']];
  });
  readonly bodyRows = computed(() => this.grid().slice(1));

  edit(row: number, column: number, text: string): void {
    this.emit(this.grid().map((cells, r) => cells.map((cell, c) => (r === row && c === column ? text : cell))));
  }

  addRow(): void {
    this.emit([...this.grid(), this.grid()[0].map(() => '')]);
  }

  removeRow(): void {
    this.emit(this.grid().slice(0, -1));
  }

  addColumn(): void {
    this.emit(this.grid().map((cells) => [...cells, '']));
  }

  removeColumn(): void {
    this.emit(this.grid().map((cells) => cells.slice(0, -1)));
  }

  text(event: Event): string {
    return inputValue(event);
  }

  private emit(grid: readonly (readonly string[])[]): void {
    this.blockChange.emit(withGrid(this.block(), grid));
  }
}
