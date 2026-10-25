import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonDirective } from '../button/button.directive';
import { LinkCtaDirective } from '../link-cta/link-cta.directive';

export type NotFoundKind = 'page-missing' | 'page-gone' | 'product-gone' | 'error';

export interface NotFoundLink {
  readonly label: string;
  readonly href: string;
}

/**
 * The "this address has no page" layout (design system NotFoundPage), used by the 404, 410 and 503 states. The HTTP status is set by
 * the container, never here; the large `404` is decorative. Errors say what to do next.
 */
@Component({
  selector: 'rp-not-found',
  imports: [RouterLink, ButtonDirective, LinkCtaDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="rp-notfound">
      @if (kind() === 'page-missing') {
        <p class="rp-notfound__code" aria-hidden="true">404</p>
      }
      <h1 class="t-display-md">
        @switch (kind()) {
          @case ('error') { Something went wrong }
          @case ('page-gone') { This page has been removed }
          @case ('product-gone') { This piece has left the collection }
          @default { We could not find that page }
        }
      </h1>
      <p class="t-body-lg">
        @switch (kind()) {
          @case ('error') { Please try again in a moment. }
          @case ('page-gone') { It is no longer part of the shop. Have a look at what is new in. }
          @case ('product-gone') { It has been retired from the shop. Have a look at what is new in. }
          @default { It may have moved, or the address may be mistyped. }
        }
      </p>
      <a rpBtn routerLink="/">Back to home</a>
      <ul class="rp-notfound__links">
        @for (l of links(); track l.href) {
          <li><a rpLink [routerLink]="l.href">{{ l.label }}</a></li>
        }
      </ul>
    </section>
  `,
  styles: `
    h1, p { margin: 0; }
    .rp-notfound__links { list-style: none; margin: 0; padding: 0; display: flex; flex-wrap: wrap; justify-content: center; gap: var(--space-4); }
  `,
})
export class NotFoundComponent {
  readonly kind = input.required<NotFoundKind>();
  readonly links = input<readonly NotFoundLink[]>([]);
}
