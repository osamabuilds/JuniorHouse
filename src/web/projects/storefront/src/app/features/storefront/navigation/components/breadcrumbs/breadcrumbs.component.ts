import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export interface Crumb {
  readonly label: string;
  /** Null for the current page: it is shown, not linked (NAV-4). */
  readonly path: string | null;
}

/** The visible trail on every page below the home page. The same list feeds the BreadcrumbList structured data (NAV-4, SEO-03). */
@Component({
  selector: 'rp-breadcrumbs',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="rp-crumbs" aria-label="Breadcrumb">
      <ol class="rp-crumbs__list">
        @for (crumb of crumbs(); track $index) {
          <li>
            @if (crumb.path !== null) {
              <a [routerLink]="crumb.path">{{ crumb.label }}</a>
            } @else {
              <span aria-current="page">{{ crumb.label }}</span>
            }
          </li>
        }
      </ol>
    </nav>
  `,
  styles: `
    .rp-crumbs__list { display: flex; flex-wrap: wrap; gap: var(--space-2); margin: 0; padding: 0; list-style: none; font: 400 13px/20px var(--font-sans); color: var(--ink-muted); }
    .rp-crumbs__list li + li::before { content: '/'; margin-right: var(--space-2); }
    .rp-crumbs__list a { text-decoration: underline; text-underline-offset: 3px; }
    .rp-crumbs__list [aria-current] { color: var(--ink); }
  `,
})
export class BreadcrumbsComponent {
  readonly crumbs = input.required<readonly Crumb[]>();
}
