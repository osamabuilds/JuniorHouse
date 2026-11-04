import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Where a person lands when they open a screen their roles do not cover (roles AC-7). It says so plainly and offers a way back. */
@Component({
  selector: 'app-forbidden-page',
  imports: [RouterLink],
  template: `
    <section class="card" aria-labelledby="fp-heading">
      <h1 id="fp-heading">You do not have access to this page</h1>
      <p>Your roles do not include it. If you need it, ask an Owner to change your roles.</p>
      <p><a routerLink="/">Back to the start</a></p>
    </section>
  `,
  styles: ':host { display: block; max-width: 36rem; margin: var(--space-6) auto; }',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ForbiddenPageContainer {}
