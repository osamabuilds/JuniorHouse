import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { LookupDto } from '@features/reference/lookups';
import { inputValue, selectNumberOrNull } from '@shared';
import { PathHistoryDto, ProductDto, SetPathValue, SlugSuggestionDto } from '../../models';

/**
 * The URL tab (URL-1..11): the product's public address, built from who it is for, its category and a short name; the slug can be
 * suggested from the product name. Changing the address of a published product keeps the old one working (a permanent redirect),
 * and the history below lists every address the product has had.
 */
@Component({
  selector: 'app-url-panel',
  imports: [DatePipe],
  template: `
    <p>Current address: <code>{{ product().path }}</code></p>

    <form class="card" (submit)="$event.preventDefault(); save()" aria-labelledby="url-heading">
      <h3 id="url-heading">Change the address</h3>
      @if (product().status === 'Published') {
        <p class="banner banner--info" role="status">
          This product is published. After the change the old address still works: shoppers and search engines are sent to the new one.
        </p>
      }
      <div class="form-grid">
        <div class="form-field">
          <label for="url-gender">For</label>
          <select id="url-gender" [disabled]="disabled()" (change)="genderId.set(number($event))">
            @for (gender of genders(); track gender.id) {
              <option [value]="gender.id" [selected]="gender.id === genderId()">{{ gender.name }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="url-category">Category</label>
          <select id="url-category" [disabled]="disabled()" (change)="categoryId.set(number($event))">
            @for (category of categories(); track category.id) {
              <option [value]="category.id" [selected]="category.id === categoryId()">{{ category.name }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="url-slug">URL name</label>
          <input id="url-slug" type="text" maxlength="80" [disabled]="disabled()" [value]="slug()" (input)="slug.set(text($event))" />
          <small class="field-hint">Lower-case letters, digits and single hyphens, for example floral-summer-dress.</small>
        </div>
      </div>
      <p>New address will be: <code>{{ preview() }}</code></p>
      @if (suggestion()) {
        <p role="status">Suggested: <code>{{ suggestion()!.slug }}</code> <button type="button" class="button button--small" (click)="slug.set(suggestion()!.slug)">Use it</button></p>
      }
      <div class="form-actions">
        <button type="button" class="button" [disabled]="disabled()" (click)="suggestRequested.emit({ genderId: genderId(), categoryId: categoryId() })">Suggest from the name</button>
        <button type="submit" class="button button--primary" [disabled]="disabled() || saving()">Change address</button>
      </div>
    </form>

    <section aria-labelledby="url-history-heading">
      <h3 id="url-history-heading">Every address this product has had</h3>
      @if (history().length === 0) {
        <p class="state-message">The product has not been published yet, so no address is being kept.</p>
      } @else {
        <ul>
          @for (entry of history(); track entry.path) {
            <li>
              <code>{{ entry.path }}</code>
              @if (entry.isCurrent) {
                <span class="badge">Current</span>
              } @else {
                <span class="badge badge--muted">Redirects to the current address</span>
              }
              <small>since {{ entry.firstUsedAt | date: 'd MMM y' }}</small>
            </li>
          }
        </ul>
      }
    </section>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UrlPanelComponent {
  readonly product = input.required<ProductDto>();
  readonly genders = input.required<readonly LookupDto[]>();
  readonly categories = input.required<readonly LookupDto[]>();
  readonly history = input.required<readonly PathHistoryDto[]>();
  readonly suggestion = input<SlugSuggestionDto | null>(null);
  readonly saving = input(false);
  readonly disabled = input(false);

  readonly saved = output<SetPathValue>();
  readonly suggestRequested = output<{ readonly genderId: number; readonly categoryId: number }>();

  readonly genderId = signal(0);
  readonly categoryId = signal(0);
  readonly slug = signal('');

  readonly preview = computed(() => {
    const gender = this.genders().find((g) => g.id === this.genderId())?.code.toLowerCase().replace(/_/g, '-') ?? '…';
    const category = this.categories().find((c) => c.id === this.categoryId())?.code.toLowerCase().replace(/_/g, '-') ?? '…';
    return `/${gender}/${category}/${this.slug() || '…'}`;
  });

  constructor() {
    effect(() => {
      const product = this.product();
      untracked(() => {
        this.genderId.set(product.genderId);
        this.categoryId.set(product.categoryId);
        this.slug.set(product.slug);
      });
    });
  }

  text(event: Event): string {
    return inputValue(event);
  }

  number(event: Event): number {
    return selectNumberOrNull(event) ?? 0;
  }

  save(): void {
    this.saved.emit({ genderId: this.genderId(), categoryId: this.categoryId(), slug: this.slug().trim() });
  }
}
