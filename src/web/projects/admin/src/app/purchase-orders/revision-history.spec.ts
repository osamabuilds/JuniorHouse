import { provideTestStore } from '@app/testing/provide-test-store';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PoRevisionDto } from './po-api.service';
import { RevisionHistory } from './revision-history';

function revision(overrides: Partial<PoRevisionDto>): PoRevisionDto {
  return {
    id: 1,
    poId: 10,
    revisionNumber: 0,
    initiatorId: 1,
    statusId: 3,
    reasonId: 11,
    impactNote: 'Baseline revision captured at Send.',
    vendorMessage: null,
    unitCost: 500,
    expectedDeliveryDate: '2026-12-01',
    latestAcceptableDate: '2026-12-15',
    overTolerancePercent: null,
    underTolerancePercent: null,
    paymentTermId: 1,
    advancePercent: 40,
    fabricResponsibilityId: 1,
    poValueBefore: 75000,
    poValueAfter: 75000,
    poValueDiff: 0,
    advanceAmountBefore: 30000,
    advanceAmountAfter: 30000,
    expectedDateShiftDays: 0,
    latestAcceptableDateShiftDays: null,
    quantityDiff: 0,
    isBeyondLatestAcceptableDate: false,
    lines: [{ sizeId: 1, colourId: 10, qty: 150 }],
    communications: null,
    ...overrides,
  };
}

describe('RevisionHistory (SCRUM-93 task 47, AC-34)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(), ...provideTestStore()] });
  });

  function render(revisions: PoRevisionDto[]): HTMLElement {
    const fixture = TestBed.createComponent(RevisionHistory);
    fixture.componentRef.setInput('revisions', revisions);
    fixture.componentRef.setInput('reasons', [{ id: 1, code: 'VendorCostIncrease', name: 'Vendor Cost Increase' }]);
    fixture.componentRef.setInput('channels', [{ id: 1, code: 'WhatsApp', name: 'WhatsApp' }]);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders one article per revision with its status and initiator', () => {
    const root = render([
      revision({}),
      revision({ id: 2, revisionNumber: 1, statusId: 2, initiatorId: 2, reasonId: 1, unitCost: 600, poValueAfter: 90000, poValueDiff: 15000 }),
    ]);

    const articles = root.querySelectorAll('article');
    expect(articles.length).toBe(2);
    expect(articles[1].textContent).toContain('Rev 1');
    expect(articles[1].textContent).toContain('In force');
    expect(articles[1].textContent).toContain('Vendor');
    expect(articles[1].textContent).toContain('Vendor Cost Increase');
  });

  it('shows the before and after of only the terms that changed', () => {
    const root = render([revision({}), revision({ id: 2, revisionNumber: 1, statusId: 2, unitCost: 600 })]);

    const rows = Array.from(root.querySelectorAll('tbody tr')).map((row) =>
      Array.from(row.querySelectorAll('th, td')).map((cell) => cell.textContent?.trim()),
    );
    expect(rows).toEqual([['Unit cost', '500 PKR', '600 PKR']]);
  });

  it('shows impact figures, the internal note and the communication behind a revision', () => {
    const root = render([
      revision({}),
      revision({
        id: 2,
        revisionNumber: 1,
        statusId: 1,
        unitCost: 600,
        poValueBefore: 75000,
        poValueAfter: 90000,
        poValueDiff: 15000,
        impactNote: 'Cost up after fabric price rise',
        vendorMessage: 'Please confirm',
        communications: [{ typeId: 2, channelId: 1, responderName: 'Ali Raza', responseDte: '2026-09-29T10:00:00Z', evidence: null }],
      }),
    ]);

    const text = (root.textContent ?? '').replace(/\s+/g, ' ');
    expect(text).toContain('PKR 75000 → PKR 90000 (+15000)');
    expect(text).toContain('Cost up after fabric price rise');
    expect(text).toContain('Please confirm');
    expect(text).toContain('Countered: Ali Raza told us via WhatsApp');
    expect(text).toContain('Ali Raza');
  });

  it('flags a delivery date beyond the latest acceptable date', () => {
    const root = render([revision({}), revision({ id: 2, revisionNumber: 1, statusId: 1, isBeyondLatestAcceptableDate: true })]);

    expect(root.querySelector('[role="note"]')?.textContent).toContain('beyond the latest acceptable date');
  });

  it('links the evidence files behind a communication', () => {
    const fixture = TestBed.createComponent(RevisionHistory);
    fixture.componentRef.setInput('revisions', [
      revision({
        communications: [
          { typeId: 1, channelId: 1, responderName: 'Ali Raza', responseDte: '2026-09-29T10:00:00Z', evidence: [{ fileId: 7, fileName: 'whatsapp.png' }] },
        ],
      }),
    ]);
    fixture.componentRef.setInput('channels', [{ id: 1, code: 'WhatsApp', name: 'WhatsApp' }]);
    fixture.componentRef.setInput('downloadUrl', (fileId: number) => `/files/${fileId}`);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement).querySelector('a') as HTMLAnchorElement;
    expect(link.textContent).toContain('Evidence: whatsapp.png');
    expect(link.getAttribute('href')).toBe('/files/7');
  });

  it('says so when there are no revisions', () => {
    expect(render([]).textContent).toContain('No revisions yet');
  });
});
