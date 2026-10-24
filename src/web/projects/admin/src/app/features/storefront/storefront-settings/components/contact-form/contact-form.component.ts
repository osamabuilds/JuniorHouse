import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { FieldErrorsComponent, inputChecked, inputValue } from '@shared';
import { MAX_POPULAR_SEARCHES } from '../../storefront-settings.constants';
import { ContactValues } from '../../utils/contact.util';
import { moveItem, removeItem } from '../../utils/list.util';
import { RowActionsComponent } from '../row-actions/row-actions.component';

/**
 * Brand, contact and announcement values, and the popular-search chips. Each field has a visible label and a hint; the phone number
 * hint shows the format the server checks (ST-4). Server messages show under the field they belong to.
 */
@Component({
  selector: 'app-contact-form',
  imports: [FieldErrorsComponent, RowActionsComponent],
  template: `
    <section class="card" aria-labelledby="cf-brand">
      <h2 id="cf-brand">Brand and contact</h2>
      <div class="form-grid">
        <div class="form-field">
          <label for="cf-name">Brand name</label>
          <input id="cf-name" type="text" maxlength="60" required autocomplete="off" [value]="values().brandName" (input)="edit({ brandName: text($event) })" />
          <app-field-errors [messages]="errors()['settings.BrandName'] ?? []" />
        </div>
        <div class="form-field">
          <label for="cf-tagline">Tagline</label>
          <input id="cf-tagline" type="text" maxlength="120" autocomplete="off" [value]="values().tagline" (input)="edit({ tagline: text($event) })" />
          <app-field-errors [messages]="errors()['settings.Tagline'] ?? []" />
        </div>
        <div class="form-field">
          <label for="cf-whatsapp">WhatsApp number</label>
          <input id="cf-whatsapp" type="tel" inputmode="tel" maxlength="20" autocomplete="off" aria-describedby="cf-whatsapp-hint" [value]="values().whatsapp" (input)="edit({ whatsapp: text($event) })" />
          <small class="field-hint" id="cf-whatsapp-hint">With the country code and no spaces, for example +923001234567.</small>
          <app-field-errors [messages]="errors()['settings.Whatsapp'] ?? []" />
        </div>
        <div class="form-field">
          <label for="cf-email">Email address</label>
          <input id="cf-email" type="email" maxlength="120" autocomplete="off" [value]="values().email" (input)="edit({ email: text($event) })" />
          <app-field-errors [messages]="errors()['settings.Email'] ?? []" />
        </div>
        <div class="form-field form-field--wide">
          <label for="cf-hours">Opening hours</label>
          <input id="cf-hours" type="text" maxlength="120" autocomplete="off" [value]="values().hours" (input)="edit({ hours: text($event) })" />
          <app-field-errors [messages]="errors()['settings.Hours'] ?? []" />
        </div>
      </div>
    </section>

    <section class="card" aria-labelledby="cf-announce">
      <h2 id="cf-announce">Announcement bar</h2>
      <div class="form-field form-field--wide">
        <label for="cf-announcement">Announcement text</label>
        <textarea id="cf-announcement" rows="2" maxlength="200" [value]="values().announcement" (input)="edit({ announcement: text($event) })"></textarea>
        <small class="field-hint">Up to 200 characters. Shown in the bar above the header while it is switched on.</small>
        <app-field-errors [messages]="errors()['settings.Announcement'] ?? []" />
      </div>
      <label class="check">
        <input type="checkbox" [checked]="values().announcementOn" (change)="edit({ announcementOn: checked($event) })" />
        Show the announcement bar
      </label>
    </section>

    <section class="card" aria-labelledby="cf-searches">
      <h2 id="cf-searches">Popular searches</h2>
      <p class="field-hint">The suggestions shown when a shopper opens search. Up to {{ max }}.</p>
      <app-field-errors [messages]="errors()['popularSearches'] ?? []" />
      <ul class="rows" aria-label="Popular searches">
        @for (term of values().popularSearches; track $index; let i = $index; let first = $first; let last = $last) {
          <li class="search-row">
            <div class="form-field">
              <label [for]="'cf-search-' + i">Search term {{ i + 1 }}</label>
              <input [id]="'cf-search-' + i" type="text" maxlength="40" autocomplete="off" [value]="term" (input)="editSearch(i, text($event))" />
            </div>
            <app-row-actions [name]="'the search term ' + (term || (i + 1))" [first]="first" [last]="last" (up)="moveSearch(i, -1)" (down)="moveSearch(i, 1)" (remove)="removeSearch(i)" />
          </li>
        }
      </ul>
      <button type="button" class="button button--small" [disabled]="values().popularSearches.length >= max" (click)="addSearch()">Add a search term</button>
    </section>
  `,
  styles: `
    :host { display: block; }
    section { margin-bottom: var(--space-4, 1rem); }
    .rows { list-style: none; margin: 0 0 var(--space-3, 0.75rem); padding: 0; }
    .search-row { margin-bottom: var(--space-3, 0.75rem); }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactFormComponent {
  readonly values = input.required<ContactValues>();
  readonly errors = input<Readonly<Record<string, readonly string[]>>>({});

  readonly valuesChange = output<ContactValues>();

  readonly max = MAX_POPULAR_SEARCHES;

  edit(patch: Partial<ContactValues>): void {
    this.valuesChange.emit({ ...this.values(), ...patch });
  }

  editSearch(index: number, term: string): void {
    this.edit({ popularSearches: this.values().popularSearches.map((t, i) => (i === index ? term : t)) });
  }

  moveSearch(index: number, delta: -1 | 1): void {
    this.edit({ popularSearches: moveItem(this.values().popularSearches, index, delta) });
  }

  removeSearch(index: number): void {
    this.edit({ popularSearches: removeItem(this.values().popularSearches, index) });
  }

  addSearch(): void {
    this.edit({ popularSearches: [...this.values().popularSearches, ''] });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  checked(event: Event): boolean {
    return inputChecked(event);
  }
}
