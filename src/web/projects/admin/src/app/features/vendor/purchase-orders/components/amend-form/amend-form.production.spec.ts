import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { setSelect, setText } from '@app/testing/production-fixtures';
import { PoDto } from '../../models';
import { AmendFormComponent } from './amend-form.component';

const po = {
  id: 10,
  poNo: 'PO-2026-00010',
  unitCost: 500,
  expectedDeliveryDate: '2026-12-01',
  latestAcceptableDate: '2026-12-15',
  overTolerancePercent: null,
  underTolerancePercent: null,
  paymentTermId: 1,
  advancePercent: 40,
  fabricResponsibilityId: 1,
  statusId: 5,
  lines: [{ sizeId: 1, colourId: 10, qty: 100 }],
} as unknown as PoDto;

const reasons = [{ id: 1, code: 'VendorCostIncrease', name: 'Vendor Cost Increase' }];
const fabricOptions = [
  { id: 1, code: 'VendorSupplied', name: 'Vendor Supplied' },
  { id: 2, code: 'RompSupplied', name: 'Romp Supplied' },
];

describe('AmendFormComponent after a pre-production sample was approved (Sprint 3 AC-27, D17)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  function create(sampleApproved: boolean, cells = [{ sizeId: 1, colourId: 10 }]) {
    const fixture = TestBed.createComponent(AmendFormComponent);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('po', po);
    fixture.componentRef.setInput('cells', cells);
    fixture.componentRef.setInput('reasons', reasons);
    fixture.componentRef.setInput('fabricOptions', fabricOptions);
    fixture.componentRef.setInput('sampleApproved', sampleApproved);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  const text = (root: HTMLElement) => (root.textContent ?? '').replace(/\s+/g, ' ');

  it('ConstructionAmendment_ShowsResampleNotice: strongly recommends a new sample when the fabric supplier changes', () => {
    const { fixture, root } = create(true);

    setSelect(root, '#amend-fabric', 2); // Romp Supplied
    fixture.detectChanges();

    expect(text(root)).toContain('A new sample is strongly recommended');
    expect(text(root)).toContain('record a new sample round under Production');
  });

  it('CommercialAmendment_ShowsCostlyNoteOnly: any change is costly, but a new sample is optional for price, dates or terms only', () => {
    const { fixture, root } = create(true);
    setText(root, '#amend-unit-cost', '650');
    fixture.detectChanges();

    expect(text(root)).toContain('A pre-production sample was already approved for this order');
    expect(text(root)).toContain('costly for the vendor');
    expect(text(root)).toContain('A new sample is optional');
    expect(text(root)).not.toContain('A new sample is strongly recommended');
  });

  it('shows no sample notice while no sample has been approved', () => {
    expect(text(create(false).root)).not.toContain('pre-production sample was already approved');
  });

  it('strongly recommends a new sample when a colour the order does not have yet is added', () => {
    const { fixture, root } = create(true, [
      { sizeId: 1, colourId: 10 },
      { sizeId: 1, colourId: 20 },
    ]);

    const inputs = Array.from(root.querySelectorAll('.line-grid input')) as HTMLInputElement[];
    inputs[1].value = '30'; // the new colour
    inputs[1].dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(text(root)).toContain('A new sample is strongly recommended');
  });

  it('does not show the notice when the form is used for a vendor counter-proposal', () => {
    const { fixture, root } = create(true);
    fixture.componentRef.setInput('mode', 'counter');
    fixture.detectChanges();

    expect(text(root)).not.toContain('A pre-production sample was already approved');
  });
});
