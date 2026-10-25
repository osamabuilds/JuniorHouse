import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { SizeAvailability, SizeOption } from '../../models/product-page.model';

const NOTES: Record<SizeAvailability, string | null> = {
  InStock: null,
  LowStock: 'Few left',
  SoldOut: 'Sold out',
  NotYetAvailable: 'Not yet available',
};

/**
 * The sizes of the chosen colour (PDP-3, 4, 5, 11). A size that can be bought is a link to the same page with `?colour=&size=`; one that
 * cannot is plain text that says why ("Sold out", "Not yet available"), struck through, and has no link, so it cannot be chosen. State is
 * always written as text, never colour alone.
 */
@Component({
  selector: 'rp-size-options',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav aria-labelledby="size-label">
      <p id="size-label" class="rp-eyebrow">Size</p>
      <ul class="sizes">
        @for (size of sizes(); track size.code) {
          <li>
            @if (size.selectable) {
              <a
                class="size"
                [routerLink]="path()"
                [queryParams]="{ colour: colour(), size: size.code }"
                [attr.aria-current]="size.code === selected() ? 'true' : null"
                replaceUrl
                >{{ size.name }}@if (note(size); as n) { <span class="size__note">{{ n }}</span> }</a
              >
            } @else {
              <span class="size size--off" aria-disabled="true">{{ size.name }}<span class="size__note">{{ note(size) }}</span></span>
            }
          </li>
        }
      </ul>
    </nav>
  `,
  styles: `
    .sizes { display: flex; flex-wrap: wrap; gap: var(--space-2); margin: var(--space-3) 0 0; padding: 0; list-style: none; }
    .size { display: inline-flex; flex-direction: column; align-items: center; justify-content: center; min-width: 56px; min-height: 48px; padding: var(--space-1) var(--space-3); border: 1px solid var(--line-strong); border-radius: var(--radius-sm); font: 400 14px/20px var(--font-sans); text-decoration: none; color: var(--ink); }
    a.size:hover { border-color: var(--ink); }
    a.size[aria-current='true'] { background: var(--ink); color: var(--surface); border-color: var(--ink); }
    a.size:focus-visible { outline: 2px solid var(--ink); outline-offset: 2px; }
    .size--off { color: var(--ink-muted); border-color: var(--line); text-decoration: line-through; }
    .size__note { font: 400 11px/14px var(--font-sans); text-decoration: none; display: block; }
    .size--off .size__note { text-decoration: none; }
  `,
})
export class SizeOptionsComponent {
  /** The product address the links point back to. */
  readonly path = input.required<string>();
  readonly sizes = input.required<readonly SizeOption[]>();
  /** The URL code of the chosen colour, kept in each size link. */
  readonly colour = input.required<string>();
  readonly selected = input<string | null>(null);

  protected note(size: SizeOption): string | null {
    return NOTES[size.availability];
  }
}
