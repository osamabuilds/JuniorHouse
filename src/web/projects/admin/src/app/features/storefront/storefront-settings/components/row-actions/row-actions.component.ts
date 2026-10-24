import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';

/**
 * Move up, move down and remove for one row of an ordered list. Plain buttons that name the row, so reordering never depends on
 * dragging and a screen reader says which row each button acts on (WCAG 2.5.7, 2.4.6; ST-15).
 */
@Component({
  selector: 'app-row-actions',
  template: `
    <div class="row-actions" role="group" [attr.aria-label]="'Actions for ' + name()">
      <button type="button" class="button button--small" [disabled]="first()" [attr.aria-label]="'Move ' + name() + ' up'" (click)="up.emit()">Move up</button>
      <button type="button" class="button button--small" [disabled]="last()" [attr.aria-label]="'Move ' + name() + ' down'" (click)="down.emit()">Move down</button>
      @if (removable()) {
        <button type="button" class="button button--small button--danger" [attr.aria-label]="'Remove ' + name()" (click)="remove.emit()">Remove</button>
      }
    </div>
  `,
  styles: '.row-actions { display: flex; flex-wrap: wrap; gap: 0.5rem; margin-top: var(--space-2, 0.5rem); }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RowActionsComponent {
  /** What the row is called, for the button names: for example "the 3 years age" or "the Help column". */
  readonly name = input.required<string>();
  readonly first = input(false);
  readonly last = input(false);
  readonly removable = input(true);

  readonly up = output<void>();
  readonly down = output<void>();
  readonly remove = output<void>();
}
