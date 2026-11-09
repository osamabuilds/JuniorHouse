import { ChangeDetectionStrategy, Component, computed, input, linkedSignal, output, signal } from '@angular/core';
import { ButtonDirective } from '../../../../../shared/ui/button/button.directive';
import { FieldComponent } from '../../../../../shared/ui/field/field.component';
import { AddressInput, City, FieldErrors, Province } from '../../models/address.model';

/** What is in the boxes: text, because that is what a box holds. */
interface Draft {
  readonly recipientName: string;
  readonly phone: string;
  readonly provinceId: string;
  readonly cityId: string;
  readonly lineOne: string;
  readonly lineTwo: string;
  readonly postcode: string;
}

type DraftKey = keyof Draft;

const toDraft = (v: AddressInput): Draft => ({
  recipientName: v.recipientName,
  phone: v.phone,
  provinceId: v.provinceId ? String(v.provinceId) : '',
  cityId: v.cityId ? String(v.cityId) : '',
  lineOne: v.lineOne,
  lineTwo: v.lineTwo ?? '',
  postcode: v.postcode ?? '',
});

/** Said when a required box is empty: what to do, not what went wrong. */
const REQUIRED: Readonly<Partial<Record<DraftKey, string>>> = {
  recipientName: 'Enter the name of the person who will receive the parcel.',
  phone: 'Enter a phone number so the courier can reach you, such as 0300 1234567.',
  provinceId: 'Choose a province.',
  cityId: 'Choose a city.',
  lineOne: 'Enter the street address, such as house number and street.',
};

/**
 * The address fields (ADD-1, ADD-4, ADD-5, ADD-8): a label on every box, the right autocomplete token, and a message that says what to do
 * beside the box that needs it. The province decides which cities are offered, so choosing one asks the container for them. It checks only
 * that required boxes are filled; the real rules (phone format, city in province, limits) are the server's, and its messages are shown here.
 */
@Component({
  selector: 'rp-address-form',
  imports: [FieldComponent, ButtonDirective],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './address-form.component.html',
  styleUrl: './address-form.component.scss',
})
export class AddressFormComponent {
  readonly value = input.required<AddressInput>();
  readonly provinces = input.required<readonly Province[]>();
  /** The cities of the chosen province. */
  readonly cities = input.required<readonly City[]>();
  /** Messages from the server, keyed by field. */
  readonly errors = input<FieldErrors>({});
  readonly saving = input(false);

  readonly submitted = output<AddressInput>();
  readonly cancelled = output<void>();
  readonly provinceChanged = output<number>();

  protected readonly draft = linkedSignal(() => toDraft(this.value()));
  private readonly attempted = signal(false);

  /** A message per box: the server's if it sent one, else "required" once a send has been tried. */
  protected readonly shown = computed((): Readonly<Partial<Record<DraftKey, string>>> => {
    const server = this.errors();
    const d = this.draft();
    const out: Partial<Record<DraftKey, string>> = {};
    for (const key of Object.keys(d) as DraftKey[]) {
      const local = this.attempted() && REQUIRED[key] && !d[key].trim() ? REQUIRED[key] : undefined;
      const message = local ?? server[key];
      if (message) out[key] = message;
    }

    return out;
  });

  protected set(key: DraftKey, event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLSelectElement).value;
    this.draft.update((d) => ({ ...d, [key]: value }));
  }

  protected chooseProvince(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    // A new province means the old city no longer applies.
    this.draft.update((d) => ({ ...d, provinceId: value, cityId: '' }));
    this.provinceChanged.emit(Number(value) || 0);
  }

  protected submit(event: Event): void {
    event.preventDefault();
    this.attempted.set(true);
    const d = this.draft();
    if (Object.keys(REQUIRED).some((k) => !d[k as DraftKey].trim())) return;

    this.submitted.emit({
      recipientName: d.recipientName.trim(),
      phone: d.phone.trim(),
      provinceId: Number(d.provinceId),
      cityId: Number(d.cityId),
      lineOne: d.lineOne.trim(),
      lineTwo: d.lineTwo.trim() || null,
      postcode: d.postcode.trim() || null,
    });
  }
}
