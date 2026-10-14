import { TestBed } from '@angular/core/testing';
import { selection } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { ApiError } from '@core/http';
import { ConfirmPutawayValue, PutawaySelection } from '../../models';
import { PutawayConfirmFormComponent } from './putaway-confirm-form.component';

describe('PutawayConfirmFormComponent (spec PUT-4, PUT-5, PUT-8, PUT-19, PUT-21)', () => {
  function create(chosen: PutawaySelection = selection(), error: ApiError | null = null) {
    const fixture = TestBed.createComponent(PutawayConfirmFormComponent);
    fixture.componentRef.setInput('selection', chosen);
    fixture.componentRef.setInput('error', error);
    fixture.componentRef.setInput('defaultPerson', 'Ayesha Khan');
    const confirmed = vi.fn<(value: ConfirmPutawayValue) => void>();
    fixture.componentInstance.confirmed.subscribe(confirmed);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const type = (id: string, value: string) => {
      const input = root.querySelector(`#${id}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const submit = () => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    return { fixture, root, confirmed, type, submit };
  }

  it('every field has a label, the quantity starts at everything pending, and the remembered name is filled in', () => {
    const { root } = create();

    for (const id of ['pc-bin', 'pc-qty', 'pc-person']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }

    expect((root.querySelector('#pc-qty') as HTMLInputElement).value).toBe('6');
    expect((root.querySelector('#pc-person') as HTMLInputElement).value).toBe('Ayesha Khan');
  });

  it('sends the bin upper-cased with the scanned barcode, the quantity and the idempotency key (PUT-4, PUT-8)', () => {
    const { confirmed, type, submit } = create();
    type('pc-bin', 'a-03-2');
    type('pc-qty', '4');

    submit();

    expect(confirmed).toHaveBeenCalledWith({
      binCode: 'A-03-2',
      quantity: 4,
      scanned: true,
      gtin: '2000000000015',
      idempotencyKey: '11111111-1111-4111-8111-111111111111',
      actingPerson: 'Ayesha Khan',
    });
  });

  it('an item chosen from the list sends scanned false and no barcode (PUT-19)', () => {
    const { confirmed, type, submit, fixture } = create(selection({ scanned: false, gtin: null }));
    type('pc-bin', 'A-03-2');

    submit();

    expect(confirmed).toHaveBeenCalledWith(expect.objectContaining({ scanned: false, gtin: null }));
    expect(textOf(fixture)).toContain('Chosen from the list, because there is no tag to scan yet.');
  });

  it('refuses more than is pending, zero, and a missing bin before anything is sent (PUT-5)', () => {
    const { confirmed, type, submit, fixture } = create();

    type('pc-bin', 'A-03-2');
    type('pc-qty', '7');
    submit();
    type('pc-qty', '0');
    submit();
    type('pc-qty', '3');
    type('pc-bin', '');
    submit();
    fixture.detectChanges();

    expect(confirmed).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
  });

  it('shows the server\'s reason in its own words, and keeps what was typed (PUT-6, PUT-21)', () => {
    const { fixture, root, type } = create(selection(), { status: 409, message: 'Only 6 unit(s) are pending for this item. Units found beyond what QC accepted are recorded as a FOUND_STOCK adjustment.', fieldErrors: {} });
    type('pc-bin', 'A-03-2');

    expect(textOf(fixture)).toContain('FOUND_STOCK adjustment');
    expect((root.querySelector('#pc-bin') as HTMLInputElement).value).toBe('A-03-2');
  });
});
