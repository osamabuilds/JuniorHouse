import { ChangeDetectionStrategy, Component } from '@angular/core';

/**
 * The frame of every sign-in screen (design system: AuthScreens). On a wide screen a full-height picture with one line on the scrim sits
 * beside the form; on a phone the form stands alone. The page puts its own `<h1>` in the projected content, and the app shell owns `<main>`
 * and shows only the slim wordmark header on these routes (route data `shell: 'slim'`).
 */
@Component({
  selector: 'rp-auth-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-auth">
      <div class="rp-auth__media" aria-hidden="true">
        <div class="rp-ph rp-ph--dusk"><span class="rp-arch"></span><span class="rp-floor"></span></div>
        <div class="rp-hero__scrim"></div>
        <p class="rp-auth__quote rp-display-sm">Clothes for children, made with care in Pakistan.</p>
      </div>
      <div class="rp-auth__main">
        <div class="rp-auth__box"><ng-content /></div>
      </div>
    </div>
  `,
  styles: ':host { display: block; }',
})
export class AuthLayoutComponent {}
