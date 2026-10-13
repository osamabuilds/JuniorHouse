import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { textOf } from '@app/testing/production-fixtures';
import { PoReceiptProgressDto } from '../../models';
import { PoReceiptsPanelComponent } from './po-receipts-panel.component';

describe('PoReceiptsPanelComponent (spec AC-40)', () => {
  const progress: PoReceiptProgressDto = {
    poId: 10,
    orderedTotal: 150,
    receivedTotal: 128,
    lines: [
      { sizeId: 1, colourId: 10, orderedQty: 100, receivedQty: 98, outstandingQty: 2 },
      { sizeId: 2, colourId: 10, orderedQty: 50, receivedQty: 30, outstandingQty: 20 },
    ],
    receipts: [
      { grnId: 1, grnNo: 'GRN-2026-00001', receivedDate: '2026-09-25', claimedTotal: 90, countedTotal: 88 },
      { grnId: 2, grnNo: 'GRN-2026-00002', receivedDate: '2026-09-27', claimedTotal: 40, countedTotal: 40 },
    ],
  };

  function create() {
    TestBed.configureTestingModule({ providers: [provideRouter([])] });
    const fixture = TestBed.createComponent(PoReceiptsPanelComponent);
    fixture.componentRef.setInput('progress', progress);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.componentRef.setInput('poNo', 'PO-2026-00010');
    fixture.detectChanges();
    return fixture;
  }

  it('shows counted against ordered per size and colour, and the outstanding pieces', () => {
    const text = textOf(create());

    expect(text).toContain('128 of 150 pieces counted in so far');
    expect(text).toContain('Size 1');
    expect(text).toContain('Still to come');
    expect(text).toContain('20');
  });

  it('lists the goods receipts behind it and links to the QC batches for the order', () => {
    const fixture = create();

    expect(textOf(fixture)).toContain('GRN-2026-00001: 88 counted of 90 claimed');
    expect(textOf(fixture)).toContain('GRN-2026-00002: 40 counted of 40 claimed');
    const link = (fixture.nativeElement as HTMLElement).querySelector('a[href^="/quality/batches"]');
    expect(link?.getAttribute('href')).toContain('search=PO-2026-00010');
  });
});
