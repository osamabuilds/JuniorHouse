import { ChangeDetectionStrategy, Component, computed, input, output } from '@angular/core';
import { FieldErrorsComponent, inputChecked, inputValue, selectValue } from '@shared';
import { NavAction, NavItem, NavKind, NavNodePath, Target } from '../../models';
import { MAX_NAV_DEPTH, canAddMember, canIndent, canMoveDown, canMoveUp, canOutdent, fieldNameAt } from '../../utils/nav-tree.util';
import { NAV_KIND_OPTIONS } from '../../storefront-settings.constants';
import { LinkTargetPickerComponent } from '../link-target-picker/link-target-picker.component';

/**
 * One navigation entry and, below it, its members (up to three levels, S-1). It is the `<li>` of a list, so the tree reads as nested
 * lists. Every action is a labelled button that names the entry (ST-15): move up or down, make it a member of the entry above, move it
 * out a level, add a member, remove. Server errors for the entry show under it, found by the entry's key path (ST-1a, ST-4).
 */
@Component({
  selector: 'li[app-nav-node]',
  imports: [LinkTargetPickerComponent, FieldErrorsComponent],
  template: `
    <fieldset class="nav-node__box" [class.is-off]="!item().enabled">
      <legend>
        {{ item().label || 'New entry' }}
        <span class="nav-node__level">level {{ level() }}</span>
        @if (!item().enabled) {
          <span class="badge badge--muted">hidden</span>
        }
      </legend>

      <div class="form-grid">
        <div class="form-field">
          <label [for]="id() + '-label'">Label</label>
          <input [id]="id() + '-label'" type="text" maxlength="40" required autocomplete="off" [value]="item().label" (input)="update({ label: text($event) })" />
          <small class="field-hint">What shoppers read, up to 40 characters.</small>
        </div>

        <div class="form-field">
          <label [for]="id() + '-kind'">Kind of entry</label>
          <select [id]="id() + '-kind'" (change)="changeKind($event)">
            @for (option of kinds; track option.value) {
              <option [value]="option.value" [selected]="option.value === item().kind">{{ option.label }}</option>
            }
          </select>
        </div>

        @if (item().kind !== 'Group') {
          <app-link-target-picker [idPrefix]="id() + '-target'" [label]="item().label || 'Entry'" [target]="item().target" (changed)="update({ target: $event })" />
        }
      </div>

      <div class="nav-node__checks">
        <label class="check">
          <input type="checkbox" [checked]="item().enabled" (change)="update({ enabled: checked($event) })" />
          Shown on the shop
        </label>
        <label class="check">
          <input type="checkbox" [checked]="item().showAges" (change)="update({ showAges: checked($event) })" />
          Add a "Shop by age" column to its menu
        </label>
      </div>
      <p class="field-hint">Key: {{ item().key }} (used in links; it does not change when you rename the entry)</p>
      <app-field-errors [messages]="errors()" />

      <div class="nav-node__actions" role="group" [attr.aria-label]="'Actions for ' + name()">
        <button type="button" class="button button--small" [disabled]="!up()" [attr.aria-label]="'Move ' + name() + ' up'" (click)="move('up')">Move up</button>
        <button type="button" class="button button--small" [disabled]="!down()" [attr.aria-label]="'Move ' + name() + ' down'" (click)="move('down')">Move down</button>
        <button type="button" class="button button--small" [disabled]="!indentable()" [attr.aria-label]="'Make ' + name() + ' a member of the entry above'" (click)="move('indent')">Make a member of the one above</button>
        <button type="button" class="button button--small" [disabled]="!outdentable()" [attr.aria-label]="'Move ' + name() + ' out one level'" (click)="move('outdent')">Move out a level</button>
        <button type="button" class="button button--small" [disabled]="!addable()" [attr.aria-label]="'Add a member to ' + name()" (click)="action.emit({ type: 'add-member', path: path() })">Add a member</button>
        <button type="button" class="button button--small button--danger" [attr.aria-label]="'Remove ' + name() + ' and its members'" (click)="action.emit({ type: 'remove', path: path() })">Remove</button>
      </div>
      @if (!addable()) {
        <p class="field-hint">Three levels is the most, so this entry cannot have members.</p>
      }
    </fieldset>

    @if (item().children.length > 0) {
      <ul class="nav-node__children" [attr.aria-label]="'Members of ' + name()">
        @for (child of item().children; track child.key; let i = $index) {
          <li app-nav-node [item]="child" [path]="childPath(i)" [root]="root()" [fieldErrors]="fieldErrors()" (action)="action.emit($event)"></li>
        }
      </ul>
    }
  `,
  styles: `
    :host { display: block; list-style: none; margin-bottom: var(--space-3, 0.75rem); }
    .nav-node__box { border: 1px solid var(--color-border, #ccc); border-radius: 0.5rem; padding: var(--space-3, 0.75rem) var(--space-4, 1rem); }
    .nav-node__box.is-off { background: var(--color-surface-muted, #f6f6f6); }
    .nav-node__level { font-size: 0.8125rem; color: var(--color-text-muted, #555); margin-inline: 0.5rem; }
    .nav-node__checks, .nav-node__actions { display: flex; flex-wrap: wrap; gap: 0.5rem 1.25rem; margin-top: var(--space-2, 0.5rem); }
    .nav-node__children { margin: var(--space-3, 0.75rem) 0 0; padding-left: var(--space-5, 1.5rem); border-left: 3px solid var(--color-border, #ddd); }
    @media (max-width: 30rem) { .nav-node__children { padding-left: var(--space-3, 0.75rem); } }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class NavNodeComponent {
  readonly item = input.required<NavItem>();
  readonly path = input.required<NavNodePath>();
  /** The whole tree, so the node can tell whether it can move or indent and what the server calls it. */
  readonly root = input.required<readonly NavItem[]>();
  readonly fieldErrors = input<Readonly<Record<string, readonly string[]>>>({});

  readonly action = output<NavAction>();

  readonly kinds = NAV_KIND_OPTIONS;

  readonly level = computed(() => this.path().length);
  readonly id = computed(() => `nn-${this.path().join('-')}`);
  readonly name = computed(() => this.item().label || 'the new entry');
  readonly errors = computed(() => this.fieldErrors()[fieldNameAt(this.root(), this.path())] ?? []);
  readonly up = computed(() => canMoveUp(this.path()));
  readonly down = computed(() => canMoveDown(this.root(), this.path()));
  readonly indentable = computed(() => canIndent(this.root(), this.path()));
  readonly outdentable = computed(() => canOutdent(this.path()));
  readonly addable = computed(() => canAddMember(this.path()) && this.path().length < MAX_NAV_DEPTH);

  childPath(index: number): NavNodePath {
    return [...this.path(), index];
  }

  update(patch: Partial<NavItem>): void {
    this.action.emit({ type: 'update', path: this.path(), patch });
  }

  move(direction: 'up' | 'down' | 'indent' | 'outdent'): void {
    this.action.emit({ type: 'move', path: this.path(), direction });
  }

  changeKind(event: Event): void {
    const kind = selectValue(event) as NavKind;
    // A group only opens its members, so it has no target; the other kinds need one.
    const target: Target | null = kind === 'Group' ? null : (this.item().target ?? { kind: 'Path', reference: '/' });
    this.update({ kind, target });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  checked(event: Event): boolean {
    return inputChecked(event);
  }
}
