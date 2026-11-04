import { isPlatformBrowser } from '@angular/common';
import { ChangeDetectionStrategy, Component, PLATFORM_ID, RESPONSE_INIT, afterNextRender, effect, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { selectBrand } from '../../../../storefront/site-settings';
import { CodResponseComponent } from '../../components/cod-response/cod-response.component';
import { CodAnswer } from '../../models/cod.model';
import { CodActions } from '../../store/cod.actions';
import { selectCodFailed, selectCodLink, selectCodOutcome, selectCodSending, selectCodStatus } from '../../store/cod.reducer';

/**
 * Where the confirm link from the email or the confirmation page lands (COD-12, COD-19). Opening the page reads the link and changes
 * nothing, so a mail scanner that opens it confirms nothing; only a button press answers. `noindex`, never cached. The token comes from the
 * address and is sent to the server, which is the only one that reads what is inside it.
 */
@Component({
  selector: 'rp-cod-confirm-page',
  imports: [RouterLink, ButtonDirective, CodResponseComponent],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="rp-wrap">
      <section class="rp-placed" aria-labelledby="cod-heading">
        <h1 class="rp-display-lg" id="cod-heading">Confirm your order</h1>
        <rp-cod-response
          [link]="link()"
          [loading]="status() === 'idle' || status() === 'loading'"
          [sending]="sending()"
          [failed]="failed()"
          [outcome]="outcome()"
          (answer)="answer($event)"
        />
        <div class="rp-placed__actions"><a rpBtn="ghost" routerLink="/">Back to the shop</a></div>
      </section>
    </div>
  `,
})
export class CodConfirmPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });
  private readonly brand = this.store.selectSignal(selectBrand);

  /** The `token` query parameter of the address. */
  protected readonly token = inject(ActivatedRoute).snapshot.queryParamMap.get('token') ?? '';

  protected readonly status = this.store.selectSignal(selectCodStatus);
  protected readonly link = this.store.selectSignal(selectCodLink);
  protected readonly sending = this.store.selectSignal(selectCodSending);
  protected readonly outcome = this.store.selectSignal(selectCodOutcome);
  protected readonly failed = this.store.selectSignal(selectCodFailed);

  constructor() {
    if (this.responseInit) {
      const headers = new Headers(this.responseInit.headers);
      headers.set('Cache-Control', 'private, no-store');
      this.responseInit.headers = headers;
    }

    effect(() => this.seo.set({ title: 'Confirm your order', description: 'Confirm your order.', path: '/cod/confirm', siteName: this.brand().name, crumbs: [], noindex: true }));

    const browser = isPlatformBrowser(inject(PLATFORM_ID));
    afterNextRender(() => {
      if (browser) this.store.dispatch(CodActions.statusRequested({ token: this.token }));
    });
  }

  protected answer(answer: CodAnswer): void {
    this.store.dispatch(CodActions.answered({ token: this.token, answer }));
  }
}
