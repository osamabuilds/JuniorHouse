import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** A product with nothing in stock stays live (PDP-16, D-2): a clear state and a way onward, and no purchase control. */
@Component({
  selector: 'rp-sold-out-state',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <aside class="sold" aria-labelledby="sold-out-heading">
      <p id="sold-out-heading" class="sold__title">Sold out</p>
      <p class="sold__text">This piece is not available right now. <a class="rp-link" routerLink="/new-in">See what is new in</a>.</p>
    </aside>
  `,
  styles: `
    .sold { padding: var(--space-4); border: 1px solid var(--line-strong); border-radius: var(--radius-sm); }
    .sold__title { margin: 0 0 var(--space-1); font: 500 12px/16px var(--font-sans); letter-spacing: 0.14em; text-transform: uppercase; }
    .sold__text { margin: 0; font: 400 15px/24px var(--font-sans); color: var(--ink-muted); }
  `,
})
export class SoldOutStateComponent {}
