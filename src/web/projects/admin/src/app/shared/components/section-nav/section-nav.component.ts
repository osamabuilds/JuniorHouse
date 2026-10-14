import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';

export interface SectionLink {
  readonly label: string;
  readonly path: string;
}

/**
 * The secondary navigation of an area with several screens (for example Labelling: Worklist, Print queue, SKU register).
 * A real <nav> landmark with a label and real links, so it works by keyboard and for assistive technology (NFR-19).
 */
@Component({
  selector: 'app-section-nav',
  imports: [RouterLink, RouterLinkActive],
  template: `
    <nav class="section-nav" [attr.aria-label]="label()">
      <ul>
        @for (link of links(); track link.path) {
          <li>
            <a [routerLink]="link.path" routerLinkActive="is-active" ariaCurrentWhenActive="page">{{ link.label }}</a>
          </li>
        }
      </ul>
    </nav>
  `,
  styles: `
    :host { display: block; margin-bottom: var(--space-4, 1rem); }
    ul { display: flex; flex-wrap: wrap; gap: 0.5rem; list-style: none; margin: 0; padding: 0; }
    a { display: inline-block; padding: 0.4rem 0.9rem; border: 1px solid var(--color-border, #ccc); border-radius: 999px; text-decoration: none; color: inherit; }
    a.is-active { background: var(--color-primary, #2a5bd7); color: #fff; border-color: transparent; }
    a:focus-visible { outline: 3px solid var(--color-focus, #ffbf47); outline-offset: 2px; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SectionNavComponent {
  readonly label = input.required<string>();
  readonly links = input.required<readonly SectionLink[]>();
}
