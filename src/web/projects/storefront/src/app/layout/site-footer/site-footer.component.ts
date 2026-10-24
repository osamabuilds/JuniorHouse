import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { selectBrand, selectFooter } from '../../features/storefront/site-settings';
import { whatsappLink } from '../../shared/utils/whatsapp';

/** Dark footer: link columns, contact line and legal row, all from the settings. No newsletter in this release. */
@Component({
  selector: 'rp-site-footer',
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './site-footer.component.html',
  styleUrl: './site-footer.component.scss',
})
export class SiteFooterComponent {
  private readonly store = inject(Store);
  protected readonly columns = this.store.selectSignal(selectFooter);
  protected readonly brand = this.store.selectSignal(selectBrand);
  protected readonly year = new Date().getFullYear();

  protected readonly whatsapp = computed(() => whatsappLink(this.brand().whatsapp));

  protected internal(href: string): boolean {
    return href.startsWith('/');
  }
}
