import { ChangeDetectionStrategy, Component, RESPONSE_INIT, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Store } from '@ngrx/store';
import { RpDialog } from '../../../../../layout/dialog/dialog.directive';
import { SeoService } from '../../../../../shared/seo/seo.service';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { BreadcrumbsComponent, Crumb } from '../../../../storefront/navigation/components/breadcrumbs/breadcrumbs.component';
import { selectBrand } from '../../../../storefront/site-settings';
import { AddressFormComponent } from '../../components/address-form/address-form.component';
import { Address, AddressInput, EMPTY_INPUT } from '../../models/address.model';
import { AddressesActions } from '../../store/addresses.actions';
import {
  selectAddressFieldErrors,
  selectAddressMessage,
  selectAddressPage,
  selectAddressSaved,
  selectAddressSaving,
  selectAddressesStatus,
  selectCitiesByProvince,
  selectProvinces,
} from '../../store/addresses.selectors';

const PATH = '/account/addresses';

const toInput = (a: Address): AddressInput => ({
  recipientName: a.recipientName, phone: a.phone, provinceId: a.provinceId, cityId: a.cityId, lineOne: a.lineOne, lineTwo: a.lineTwo, postcode: a.postcode,
});

/**
 * The address book (ADD-1 to ADD-8): the customer's saved addresses, one panel each, with add, edit, make default and remove. The page is
 * for one person, so it is `noindex` and its response is never cached (SEO-05, NFR-26). Server-rendered like every route; the data comes
 * from the store, which asks the API through the same-origin pass-through.
 */
@Component({
  selector: 'rp-addresses-page',
  imports: [BreadcrumbsComponent, AddressFormComponent, ButtonDirective, RpDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './addresses-page.container.html',
  styleUrl: './addresses-page.container.scss',
})
export class AddressesPageContainer {
  private readonly store = inject(Store);
  private readonly seo = inject(SeoService);
  private readonly responseInit = inject(RESPONSE_INIT, { optional: true });

  private readonly formDialog = viewChild<RpDialog>('formDialog');
  private readonly confirmDialog = viewChild<RpDialog>('confirmDialog');

  protected readonly status = this.store.selectSignal(selectAddressesStatus);
  protected readonly page = this.store.selectSignal(selectAddressPage);
  protected readonly saving = this.store.selectSignal(selectAddressSaving);
  protected readonly fieldErrors = this.store.selectSignal(selectAddressFieldErrors);
  protected readonly message = this.store.selectSignal(selectAddressMessage);
  private readonly saved = this.store.selectSignal(selectAddressSaved);
  protected readonly provinces = this.store.selectSignal(selectProvinces);
  private readonly citiesByProvince = this.store.selectSignal(selectCitiesByProvince);
  private readonly brand = this.store.selectSignal(selectBrand);

  /** The address being edited, or null when adding. */
  protected readonly editing = signal<Address | null>(null);
  protected readonly formValue = signal<AddressInput>(EMPTY_INPUT);
  protected readonly formOpen = signal(false);
  protected readonly toRemove = signal<Address | null>(null);
  private readonly chosenProvince = signal(0);

  protected readonly cities = computed(() => this.citiesByProvince()[this.chosenProvince()] ?? []);
  protected readonly crumbs: readonly Crumb[] = [{ label: 'Home', path: '/' }, { label: 'Account', path: '/account/addresses' }, { label: 'Addresses', path: null }];
  protected readonly canAdd = computed(() => this.status() === 'ready');

  constructor() {
    this.noCache();
    this.store.dispatch(AddressesActions.load({ page: 1 }));
    this.store.dispatch(AddressesActions.loadProvinces());

    effect(() => {
      this.seo.set({
        title: 'Addresses',
        description: 'Your saved delivery addresses.',
        path: PATH,
        siteName: this.brand().name,
        crumbs: [{ name: 'Account', path: '/account/addresses' }, { name: 'Addresses', path: PATH }],
        noindex: true,
      });
    });

    // A save that went through closes its dialog; the store has already asked for the list again.
    effect(() => {
      if (this.saved()) {
        this.formDialog()?.close();
        this.store.dispatch(AddressesActions.clearFeedback());
      }
    });
  }

  protected add(): void {
    this.store.dispatch(AddressesActions.clearFeedback());
    this.editing.set(null);
    this.formValue.set(EMPTY_INPUT);
    this.chosenProvince.set(0);
    this.openForm();
  }

  protected edit(address: Address): void {
    this.store.dispatch(AddressesActions.clearFeedback());
    this.editing.set(address);
    this.formValue.set(toInput(address));
    this.chooseProvince(address.provinceId);
    this.openForm();
  }

  protected chooseProvince(provinceId: number): void {
    this.chosenProvince.set(provinceId);
    this.store.dispatch(AddressesActions.loadCities({ provinceId }));
  }

  protected save(input: AddressInput): void {
    this.store.dispatch(AddressesActions.save({ id: this.editing()?.id ?? null, input }));
  }

  protected makeDefault(address: Address): void {
    this.store.dispatch(AddressesActions.makeDefault({ id: address.id }));
  }

  protected askRemove(address: Address): void {
    this.store.dispatch(AddressesActions.clearFeedback());
    this.toRemove.set(address);
    this.confirmDialog()?.open();
  }

  protected confirmRemove(): void {
    const address = this.toRemove();
    if (address) this.store.dispatch(AddressesActions.remove({ id: address.id }));
    this.confirmDialog()?.close();
  }

  protected goTo(page: number): void {
    this.store.dispatch(AddressesActions.load({ page }));
  }

  protected reload(): void {
    this.store.dispatch(AddressesActions.load({ page: 1 }));
  }

  protected closeForm(): void {
    this.formDialog()?.close();
  }

  protected formClosed(): void {
    this.formOpen.set(false);
    this.store.dispatch(AddressesActions.clearFeedback());
  }

  private openForm(): void {
    this.formOpen.set(true);
    this.formDialog()?.open();
  }

  /** One customer's data: no shared cache or browser history may keep the page (NFR-26). */
  private noCache(): void {
    if (!this.responseInit) return;
    const headers = new Headers(this.responseInit.headers);
    headers.set('Cache-Control', 'private, no-store');
    this.responseInit.headers = headers;
  }
}
