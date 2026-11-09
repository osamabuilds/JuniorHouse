import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AddressInput, City, EMPTY_INPUT, FieldErrors, Province } from '../../models/address.model';
import { AddressFormComponent } from './address-form.component';

const provinces: Province[] = [{ id: 1, code: 'PB', name: 'Punjab' }, { id: 2, code: 'SD', name: 'Sindh' }];
const cities: City[] = [{ id: 10, code: 'LHE', name: 'Lahore', provinceId: 1 }];

@Component({
  imports: [AddressFormComponent],
  template: `<rp-address-form
    [value]="value()"
    [provinces]="provinces"
    [cities]="cities()"
    [errors]="errors()"
    [saving]="saving()"
    (submitted)="sent.push($event)"
    (cancelled)="cancelled = cancelled + 1"
    (provinceChanged)="provinceChanges.push($event)"
  />`,
})
class HostComponent {
  value = signal<AddressInput>(EMPTY_INPUT);
  cities = signal<readonly City[]>([]);
  errors = signal<FieldErrors>({});
  saving = signal(false);
  provinces = provinces;
  sent: AddressInput[] = [];
  provinceChanges: number[] = [];
  cancelled = 0;
}

describe('AddressFormComponent', () => {
  function setup() {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    const el: HTMLElement = fixture.nativeElement;
    const type = (id: string, text: string) => {
      const input = el.querySelector<HTMLInputElement>(`[name="${id}"]`)!;
      input.value = text;
      input.dispatchEvent(new Event('input'));
    };
    return { fixture, host: fixture.componentInstance, el, type };
  }

  it('ADD-8 every control has a label tied to it, and the right autocomplete token', () => {
    const { el } = setup();
    const expected: Record<string, string> = {
      recipientName: 'name', phone: 'tel', provinceId: 'address-level1', cityId: 'address-level2', lineOne: 'address-line1', lineTwo: 'address-line2', postcode: 'postal-code',
    };
    for (const [name, token] of Object.entries(expected)) {
      const control = el.querySelector<HTMLElement>(`[name="${name}"]`)!;
      expect(control, name).toBeTruthy();
      expect(control.getAttribute('autocomplete'), name).toBe(token);
      expect(el.querySelector(`label[for="${control.id}"]`), name).toBeTruthy();
    }
  });

  it('ADD-8 an empty submit sends nothing and says what to fix, linked to the field', () => {
    const { el, host, fixture } = setup();
    el.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    expect(host.sent).toEqual([]);
    const phone = el.querySelector<HTMLInputElement>('[name="phone"]')!;
    expect(phone.getAttribute('aria-invalid')).toBe('true');
    const error = phone.closest('.rp-field')!.querySelector('.rp-error')!;
    expect(error.textContent).toMatch(/phone/i);
    expect(phone.getAttribute('aria-describedby')).toContain(error.id);
  });

  it('ADD-4 choosing a province asks for its cities and clears the city', () => {
    const { el, host, fixture } = setup();
    const province = el.querySelector<HTMLSelectElement>('[name="provinceId"]')!;
    province.value = '1';
    province.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    expect(host.provinceChanges).toEqual([1]);
    expect(el.querySelector<HTMLSelectElement>('[name="cityId"]')!.value).toBe('');
  });

  it('ADD-1 a complete form sends the trimmed fields, with empty optional lines as null', () => {
    const { host, el, type, fixture } = setup();
    const province = el.querySelector<HTMLSelectElement>('[name="provinceId"]')!;
    province.value = '1';
    province.dispatchEvent(new Event('change'));
    host.cities.set(cities);
    fixture.detectChanges();
    const city = el.querySelector<HTMLSelectElement>('[name="cityId"]')!;
    city.value = '10';
    city.dispatchEvent(new Event('change'));
    type('recipientName', '  Ayesha Khan ');
    type('phone', '0300 1234567');
    type('lineOne', ' House 12, Street 4 ');
    el.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit'));
    expect(host.sent).toEqual([
      { recipientName: 'Ayesha Khan', phone: '0300 1234567', provinceId: 1, cityId: 10, lineOne: 'House 12, Street 4', lineTwo: null, postcode: null },
    ]);
  });

  it('ADD-5 messages from the server appear beside their fields', () => {
    const { host, el, fixture } = setup();
    host.errors.set({ phone: 'Enter a Pakistani phone number such as 0300 1234567.' });
    fixture.detectChanges();
    expect(el.querySelector('.rp-error')?.textContent).toContain('Pakistani phone number');
  });

  it('ADD-8 cancel is a button, and saving disables the submit with a reason', () => {
    const { host, el, fixture } = setup();
    el.querySelector<HTMLButtonElement>('button[data-cancel]')!.click();
    expect(host.cancelled).toBe(1);
    host.saving.set(true);
    fixture.detectChanges();
    expect(el.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(true);
  });
});
