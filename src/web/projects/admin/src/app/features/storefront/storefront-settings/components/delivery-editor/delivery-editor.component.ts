import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FieldErrorsComponent, inputChecked, inputValue } from '@shared';
import { DeliveryRule } from '../../models';
import { DeliveryValues, parseAmount } from '../../utils/delivery.util';
import { moveItem, removeItem, replaceItem } from '../../utils/list.util';
import { RowActionsComponent } from '../row-actions/row-actions.component';

/**
 * Delivery by city (days, charge in PKR, free-delivery amount, courier) and the exchange policy (window and conditions). Money boxes are
 * number inputs with a step of one paisa; a negative amount or a longest time shorter than the shortest is refused by the server with a
 * message under the city (ST-4, ST-7).
 */
@Component({
  selector: 'app-delivery-editor',
  imports: [FieldErrorsComponent, RowActionsComponent],
  template: `
    <section aria-labelledby="de-delivery">
      <h2 id="de-delivery">Delivery</h2>
      <ul class="rows" aria-label="Delivery by city">
        @for (rule of values().rules; track $index; let i = $index) {
          <li class="card">
            <div class="form-grid">
              <div class="form-field">
                <label [for]="'dr-city-' + i">City</label>
                <input [id]="'dr-city-' + i" type="text" maxlength="60" required autocomplete="off" [value]="rule.city" (input)="editRule(i, { city: text($event) })" />
              </div>
              <div class="form-field">
                <label [for]="'dr-min-' + i">Fewest days</label>
                <input [id]="'dr-min-' + i" type="number" min="0" step="1" [value]="rule.minDays" (input)="editRule(i, { minDays: whole($event) })" />
              </div>
              <div class="form-field">
                <label [for]="'dr-max-' + i">Most days</label>
                <input [id]="'dr-max-' + i" type="number" min="0" step="1" [value]="rule.maxDays" (input)="editRule(i, { maxDays: whole($event) })" />
              </div>
              <div class="form-field">
                <label [for]="'dr-charge-' + i">Delivery charge (PKR)</label>
                <input [id]="'dr-charge-' + i" type="number" min="0" step="0.01" [value]="rule.charge" (input)="editRule(i, { charge: money($event) })" />
              </div>
              <div class="form-field">
                <label [for]="'dr-free-' + i">Free delivery over (PKR)</label>
                <input [id]="'dr-free-' + i" type="number" min="0" step="0.01" [value]="rule.freeOver ?? ''" aria-describedby="dr-free-hint" (input)="editRule(i, { freeOver: amount($event) })" />
                <small class="field-hint" id="dr-free-hint">Leave blank for no free delivery.</small>
              </div>
              <div class="form-field">
                <label [for]="'dr-courier-' + i">Courier</label>
                <input [id]="'dr-courier-' + i" type="text" maxlength="60" autocomplete="off" [value]="rule.courier" (input)="editRule(i, { courier: text($event) })" />
              </div>
              <div class="form-field">
                <label class="check">
                  <input type="checkbox" [checked]="rule.enabled" (change)="editRule(i, { enabled: checked($event) })" />
                  Delivers here
                </label>
              </div>
            </div>
            <app-field-errors [messages]="errors()['delivery.' + rule.city] ?? []" />
            <app-row-actions [name]="'the ' + (rule.city || 'new') + ' row'" [first]="i === 0" [last]="i === values().rules.length - 1" (up)="moveRule(i, -1)" (down)="moveRule(i, 1)" (remove)="removeRule(i)" />
          </li>
        } @empty {
          <li class="state-message">No cities yet. Add the first one.</li>
        }
      </ul>
      <button type="button" class="button" (click)="addRule.emit()">Add a city</button>
    </section>

    <section aria-labelledby="de-exchange" class="exchange">
      <h2 id="de-exchange">Exchanges</h2>
      <div class="form-field">
        <label for="de-window">Exchange window (days)</label>
        <input id="de-window" type="number" min="1" max="365" step="1" [value]="values().windowDays ?? ''" aria-describedby="de-window-hint" (input)="edit({ windowDays: wholeOrNull($event) })" />
        <small class="field-hint" id="de-window-hint">How many days after delivery a shopper can ask for an exchange.</small>
        <app-field-errors [messages]="errors()['exchange.window'] ?? []" />
      </div>

      <h3>Conditions</h3>
      <app-field-errors [messages]="errors()['exchange.conditions'] ?? []" />
      <ul class="rows" aria-label="Exchange conditions">
        @for (condition of values().conditions; track $index; let i = $index) {
          <li class="condition">
            <div class="form-field">
              <label [for]="'de-cond-' + i">Condition {{ i + 1 }}</label>
              <input [id]="'de-cond-' + i" type="text" maxlength="300" autocomplete="off" [value]="condition" (input)="editCondition(i, text($event))" />
            </div>
            <app-row-actions [name]="'condition ' + (i + 1)" [first]="i === 0" [last]="i === values().conditions.length - 1" (up)="moveCondition(i, -1)" (down)="moveCondition(i, 1)" (remove)="removeCondition(i)" />
          </li>
        }
      </ul>
      <button type="button" class="button button--small" (click)="addCondition()">Add a condition</button>
    </section>
  `,
  styles: `
    :host { display: block; }
    .rows { list-style: none; margin: 0 0 var(--space-3, 0.75rem); padding: 0; }
    .exchange { margin-top: var(--space-5, 1.5rem); }
    .condition { margin-bottom: var(--space-3, 0.75rem); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeliveryEditorComponent {
  readonly values = input.required<DeliveryValues>();
  readonly errors = input<Readonly<Record<string, readonly string[]>>>({});

  readonly valuesChange = output<DeliveryValues>();
  readonly addRule = output<void>();

  edit(patch: Partial<DeliveryValues>): void {
    this.valuesChange.emit({ ...this.values(), ...patch });
  }

  editRule(index: number, patch: Partial<DeliveryRule>): void {
    this.edit({ rules: replaceItem(this.values().rules, index, patch) });
  }

  moveRule(index: number, delta: -1 | 1): void {
    this.edit({ rules: moveItem(this.values().rules, index, delta) });
  }

  removeRule(index: number): void {
    this.edit({ rules: removeItem(this.values().rules, index) });
  }

  editCondition(index: number, text: string): void {
    this.edit({ conditions: this.values().conditions.map((c, i) => (i === index ? text : c)) });
  }

  moveCondition(index: number, delta: -1 | 1): void {
    this.edit({ conditions: moveItem(this.values().conditions, index, delta) });
  }

  removeCondition(index: number): void {
    this.edit({ conditions: removeItem(this.values().conditions, index) });
  }

  addCondition(): void {
    this.edit({ conditions: [...this.values().conditions, ''] });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  checked(event: Event): boolean {
    return inputChecked(event);
  }

  whole(event: Event): number {
    return Math.trunc(parseAmount(inputValue(event)) ?? 0);
  }

  wholeOrNull(event: Event): number | null {
    const value = parseAmount(inputValue(event));
    return value === null ? null : Math.trunc(value);
  }

  money(event: Event): number {
    return parseAmount(inputValue(event)) ?? 0;
  }

  amount(event: Event): number | null {
    return parseAmount(inputValue(event));
  }
}
