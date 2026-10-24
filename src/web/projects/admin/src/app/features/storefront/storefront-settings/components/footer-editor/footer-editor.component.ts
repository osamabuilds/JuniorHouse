import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FieldErrorsComponent, inputChecked, inputValue } from '@shared';
import { FooterColumn, FooterLink, Target } from '../../models';
import { moveItem, removeItem, replaceItem, withSort } from '../../utils/list.util';
import { LinkTargetPickerComponent } from '../link-target-picker/link-target-picker.component';
import { RowActionsComponent } from '../row-actions/row-actions.component';

/** The footer: titled columns of links. Order, visibility and removal are buttons; every link target is typed and checked (ST-1, ST-4). */
@Component({
  selector: 'app-footer-editor',
  imports: [FieldErrorsComponent, LinkTargetPickerComponent, RowActionsComponent],
  template: `
    <ul class="rows" aria-label="Footer columns">
      @for (column of columns(); track $index; let ci = $index; let firstColumn = $first; let lastColumn = $last) {
        <li class="card">
          <h2 class="column-title">{{ column.title || 'New column' }}</h2>
          <div class="form-field">
            <label [for]="'fc-title-' + ci">Column title</label>
            <input [id]="'fc-title-' + ci" type="text" maxlength="40" required autocomplete="off" [value]="column.title" (input)="editColumn(ci, { title: text($event) })" />
          </div>
          <app-field-errors [messages]="errors()['footer.' + column.title] ?? []" />
          <app-row-actions
            [name]="'the ' + (column.title || 'new') + ' column'"
            [first]="firstColumn"
            [last]="lastColumn"
            (up)="moveColumn(ci, -1)"
            (down)="moveColumn(ci, 1)"
            (remove)="removeColumn(ci)"
          />

          <ul class="rows rows--nested" [attr.aria-label]="'Links in ' + (column.title || 'the new column')">
            @for (link of column.links; track $index; let li = $index; let firstLink = $first; let lastLink = $last) {
              <li class="card card--inner">
                <div class="form-grid">
                  <div class="form-field">
                    <label [for]="'fl-label-' + ci + '-' + li">Link text</label>
                    <input [id]="'fl-label-' + ci + '-' + li" type="text" maxlength="40" required autocomplete="off" [value]="link.label" (input)="editLink(ci, li, { label: text($event) })" />
                  </div>
                  <app-link-target-picker [idPrefix]="'fl-' + ci + '-' + li" [label]="link.label || 'Link'" [target]="link.target" (changed)="editLink(ci, li, { target: $event })" />
                  <div class="form-field">
                    <label class="check">
                      <input type="checkbox" [checked]="link.enabled" (change)="editLink(ci, li, { enabled: checked($event) })" />
                      Shown on the shop
                    </label>
                  </div>
                </div>
                <app-field-errors [messages]="errors()['footer.' + column.title + '.' + link.label] ?? []" />
                <app-row-actions
                  [name]="'the ' + (link.label || 'new') + ' link in ' + (column.title || 'the new column')"
                  [first]="firstLink"
                  [last]="lastLink"
                  (up)="moveLink(ci, li, -1)"
                  (down)="moveLink(ci, li, 1)"
                  (remove)="removeLink(ci, li)"
                />
              </li>
            }
          </ul>
          <button type="button" class="button button--small" (click)="addLink(ci)">Add a link to {{ column.title || 'this column' }}</button>
        </li>
      } @empty {
        <li class="state-message">There are no footer columns yet.</li>
      }
    </ul>
  `,
  styles: `
    .rows { list-style: none; margin: 0; padding: 0; }
    .rows--nested { margin: var(--space-3, 0.75rem) 0; }
    .card--inner { margin-bottom: var(--space-3, 0.75rem); }
    .column-title { margin-top: 0; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class FooterEditorComponent {
  readonly columns = input.required<readonly FooterColumn[]>();
  readonly errors = input<Readonly<Record<string, readonly string[]>>>({});

  readonly columnsChange = output<readonly FooterColumn[]>();

  editColumn(index: number, patch: Partial<FooterColumn>): void {
    this.columnsChange.emit(replaceItem(this.columns(), index, patch));
  }

  moveColumn(index: number, delta: -1 | 1): void {
    this.columnsChange.emit(withSort(moveItem(this.columns(), index, delta)));
  }

  removeColumn(index: number): void {
    this.columnsChange.emit(withSort(removeItem(this.columns(), index)));
  }

  addLink(columnIndex: number): void {
    const column = this.columns()[columnIndex];
    const link: FooterLink = { label: '', target: { kind: 'Path', reference: '/' }, sort: column.links.length + 1, enabled: true };
    this.editColumn(columnIndex, { links: withSort([...column.links, link]) });
  }

  editLink(columnIndex: number, linkIndex: number, patch: Partial<FooterLink> & { target?: Target }): void {
    const column = this.columns()[columnIndex];
    this.editColumn(columnIndex, { links: replaceItem(column.links, linkIndex, patch) });
  }

  moveLink(columnIndex: number, linkIndex: number, delta: -1 | 1): void {
    const column = this.columns()[columnIndex];
    this.editColumn(columnIndex, { links: withSort(moveItem(column.links, linkIndex, delta)) });
  }

  removeLink(columnIndex: number, linkIndex: number): void {
    const column = this.columns()[columnIndex];
    this.editColumn(columnIndex, { links: withSort(removeItem(column.links, linkIndex)) });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  checked(event: Event): boolean {
    return inputChecked(event);
  }
}
