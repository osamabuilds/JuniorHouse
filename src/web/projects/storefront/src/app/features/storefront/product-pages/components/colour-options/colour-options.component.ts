import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ColourOption } from '../../models/product-page.model';

/**
 * The colours of the product (PDP-2, 11). Each is a link to the same page with `?colour=`, so choosing works without scripting and the
 * router swaps the page in place with it. Reference has no swatch colour, so a colour is shown by its name, and the chosen one is marked
 * by border, weight and `aria-current`, never by colour alone. A colour with nothing in stock says so in words.
 */
@Component({
  selector: 'rp-colour-options',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-labelledby="colour-label">
      <p id="colour-label" class="rp-eyebrow">Colour: {{ selectedName() }}</p>
      <ul class="colours">
        @for (colour of colours(); track colour.code) {
          <li>
            <a
              class="colour"
              [routerLink]="path()"
              [queryParams]="{ colour: colour.code }"
              [attr.aria-current]="colour.code === selected() ? 'true' : null"
              replaceUrl
              >{{ colour.name }}@if (!colour.inStock) { <span class="colour__note"> (sold out)</span> }</a
            >
          </li>
        }
      </ul>
    </nav>
  `,
  styles: `
    .colours { display: flex; flex-wrap: wrap; gap: var(--space-2); margin: var(--space-3) 0 0; padding: 0; list-style: none; }
    .colour { display: inline-flex; align-items: center; min-height: 44px; padding: 0 var(--space-4); border: 1px solid var(--line-strong); border-radius: var(--radius-sm); font: 400 14px/20px var(--font-sans); text-decoration: none; color: var(--ink); }
    .colour:hover { border-color: var(--ink); }
    .colour[aria-current='true'] { border-color: var(--ink); box-shadow: inset 0 0 0 1px var(--ink); font-weight: 500; }
    .colour:focus-visible { outline: 2px solid var(--ink); outline-offset: 2px; }
    .colour__note { color: var(--ink-muted); }
  `,
})
export class ColourOptionsComponent {
  /** The product address the links point back to. */
  readonly path = input.required<string>();
  readonly colours = input.required<readonly ColourOption[]>();
  /** The URL code of the chosen colour. */
  readonly selected = input.required<string>();

  protected selectedName(): string {
    return this.colours().find((c) => c.code === this.selected())?.name ?? '';
  }
}
