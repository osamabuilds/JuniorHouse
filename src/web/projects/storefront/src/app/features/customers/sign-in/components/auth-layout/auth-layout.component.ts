import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * The frame of every sign-in screen (design system: AuthScreens). On a wide screen a quiet panel with one line sits beside the form; on a
 * phone the form stands alone. The page puts its own `<h1>` in the projected content, and the app shell owns `<main>`.
 */
@Component({
  selector: 'rp-auth-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-auth">
      <div class="rp-auth__media" aria-hidden="true">
        <p class="rp-auth__quote">{{ quote() }}</p>
      </div>
      <section class="rp-auth__main">
        <div class="rp-auth__box"><ng-content /></div>
      </section>
    </div>
  `,
  styles: `
    :host { display: block; }
    .rp-auth__media { padding: var(--space-8); }
    .rp-auth__quote { position: absolute; left: var(--space-8); right: var(--space-8); bottom: var(--space-8); margin: 0; font: 600 clamp(24px, 2.4vw, 32px)/1.2 var(--font-display); }
  `,
})
export class AuthLayoutComponent {
  readonly quote = input('Considered clothes for children who move.');
}
