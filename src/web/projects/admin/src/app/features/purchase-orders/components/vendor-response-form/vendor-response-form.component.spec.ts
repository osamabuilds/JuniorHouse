import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { PoDto, VendorResponseValue } from '../../models';
import { VendorResponseFormComponent } from './vendor-response-form.component';

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

describe('VendorResponseFormComponent (SCRUM-93 task 49, AC-25..AC-31)', () => {
  function create() {
    const fixture = TestBed.createComponent(VendorResponseFormComponent);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
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
