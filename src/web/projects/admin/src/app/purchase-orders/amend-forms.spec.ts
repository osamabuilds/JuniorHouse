import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AmendForm } from './amend-form';
import { AmendmentValue, PoDto, VendorResponseValue } from './po-api.service';
import { VendorResponseForm } from './vendor-response-form';

const po = {
  id: 10,
  poNo: 'PO-2026-00001',
  unitCost: 500,
  expectedDeliveryDate: '2026-12-01',
  latestAcceptableDate: '2026-12-15',
  overTolerancePercent: null,
  underTolerancePercent: null,
  paymentTermId: 1,
  advancePercent: 40,
  fabricResponsibilityId: 1,
  statusId: 3,
  lines: [{ sizeId: 1, colourId: 10, qty: 100 }],
} as unknown as PoDto;

const reasons = [{ id: 1, code: 'VendorCostIncrease', name: 'Vendor Cost Increase' }];
const channels = [{ id: 1, code: 'WhatsApp', name: 'WhatsApp' }];
const cells = [
  { sizeId: 1, colourId: 10 },
  { sizeId: 2, colourId: 10 },
];

function setSelect(root: HTMLElement, selector: string, index: number): void {
  const select = root.querySelector(selector) as HTMLSelectElement;
  select.selectedIndex = index;
  select.dispatchEvent(new Event('change'));
}

function setText(root: HTMLElement, selector: string, value: string): void {
  const element = root.querySelector(selector) as HTMLInputElement | HTMLTextAreaElement;
  element.value = value;
  element.dispatchEvent(new Event('input'));
}

beforeEach(() => {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
});

describe('AmendForm (SCRUM-93 task 48, AC-15..AC-17)', () => {
  function create() {
    const fixture = TestBed.createComponent(AmendForm);
    fixture.componentRef.setInput('po', po);
    fixture.componentRef.setInput('cells', cells);
    fixture.componentRef.setInput('reasons', reasons);
    fixture.detectChanges();
    return fixture;
  }

  it('starts from the PO’s current terms and quantities', () => {
    const root = create().nativeElement as HTMLElement;

    expect((root.querySelector('#amend-unit-cost') as HTMLInputElement).value).toBe('500');
    expect((root.querySelector('#amend-delivery') as HTMLInputElement).value).toBe('2026-12-01');
    const quantities = Array.from(root.querySelectorAll('.line-grid input')).map((i) => (i as HTMLInputElement).value);
    expect(quantities).toEqual(['100', '0']);
  });

  it('will not submit without a reason and an internal note, and says what is missing', () => {
    const fixture = create();
    const amended = vi.fn();
    fixture.componentInstance.amended.subscribe(amended);
    const root = fixture.nativeElement as HTMLElement;

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(amended).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Choose a reason for this change.');
    expect(root.textContent).toContain('Explain the impact of this change for the record.');
  });

  it('submits the changed terms, dropping zero-quantity lines', () => {
    const fixture = create();
    let value: AmendmentValue | undefined;
    fixture.componentInstance.amended.subscribe((v) => (value = v));
    const root = fixture.nativeElement as HTMLElement;

    setSelect(root, '#amend-reason', 1);
    setText(root, '#amend-note', 'Fabric price rise');
    setText(root, '#amend-unit-cost', '600');
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(value?.unitCost).toBe(600);
    expect(value?.reasonId).toBe(1);
    expect(value?.impactNote).toBe('Fabric price rise');
    expect(value?.initiatorId).toBe(1);
    expect(value?.lines).toEqual([{ sizeId: 1, colourId: 10, qty: 100 }]);
  });

  it('shows the API’s real message when the server rejects the amendment', () => {
    const fixture = create();
    fixture.componentRef.setInput('error', {
      status: 400,
      message: 'This amendment doesn’t change anything from the PO’s current terms or lines.',
      fieldErrors: {},
    });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('doesn’t change anything');
  });
});

describe('VendorResponseForm (SCRUM-93 task 49, AC-25..AC-31)', () => {
  function create() {
    const fixture = TestBed.createComponent(VendorResponseForm);
    fixture.componentRef.setInput('po', po);
    fixture.componentRef.setInput('currentRevisionNumber', 0);
    fixture.componentRef.setInput('channels', channels);
    fixture.componentRef.setInput('reasons', reasons);
    fixture.componentRef.setInput('cells', cells);
    fixture.detectChanges();
    return fixture;
  }

  it('requires the channel and who responded before recording', () => {
    const fixture = create();
    const recorded = vi.fn();
    fixture.componentInstance.recorded.subscribe(recorded);
    const root = fixture.nativeElement as HTMLElement;

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(recorded).not.toHaveBeenCalled();
    expect(root.textContent).toContain('Choose the channel the vendor used.');
    expect(root.textContent).toContain('Enter the name of the person who responded.');
  });

  it('records a confirmation against the current revision', () => {
    const fixture = create();
    let value: VendorResponseValue | undefined;
    fixture.componentInstance.recorded.subscribe((v) => (value = v));
    const root = fixture.nativeElement as HTMLElement;

    setSelect(root, '#vresp-channel', 1);
    setText(root, '#vresp-responder', 'Ali Raza');
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(value).toMatchObject({ outcomeTypeId: 1, revisionNumber: 0, channelId: 1, responderName: 'Ali Raza', counter: null });
  });

  it('opens the counter-proposal form, flagged vendor-initiated, when the vendor countered', () => {
    const fixture = create();
    const root = fixture.nativeElement as HTMLElement;

    setSelect(root, '#vresp-outcome', 1);
    fixture.detectChanges();

    expect(root.querySelector('app-amend-form')).not.toBeNull();
    expect(root.textContent).toContain('Counter-proposal from the vendor');
    // the counter form replaces the simple submit, so there is exactly one primary action
    expect(root.querySelectorAll('.button--primary').length).toBe(1);
  });
});
