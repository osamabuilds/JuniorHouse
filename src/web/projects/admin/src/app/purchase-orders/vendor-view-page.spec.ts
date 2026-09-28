import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { VendorViewPage } from './vendor-view-page';

describe('VendorViewPage (SCRUM-93 task 51, AC-35/AC-36)', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  });

  function create() {
    const fixture = TestBed.createComponent(VendorViewPage);
    fixture.componentRef.setInput('id', '10');
    fixture.detectChanges();
    return { fixture, http: TestBed.inject(HttpTestingController) };
  }

  it('renders the agreed terms, quantities, files and a pending-change notice, with no admin chrome', () => {
    const { fixture, http } = create();
    http.expectOne((r) => r.url.endsWith('/api/purchase-orders/10/vendor-view')).flush({
      poNo: 'PO-2026-00001',
      vendorName: 'Test Vendor',
      styleId: 5,
      statusId: 3,
      revisionNumber: 0,
      unitCost: 500,
      expectedDeliveryDate: '2026-12-01',
      latestAcceptableDate: '2026-12-15',
      overTolerancePercent: 5,
      underTolerancePercent: null,
      paymentTermId: 1,
      advancePercent: 40,
      fabricResponsibilityId: 1,
      lines: [{ sizeId: 1, colourId: 10, qty: 100 }],
      files: [{ id: 3, fileName: 'spec.pdf', categoryId: 1 }],
      pendingRevision: {
        revisionNumber: 1,
        initiatorId: 1,
        vendorMessage: 'Please confirm',
        unitCost: 600,
        expectedDeliveryDate: '2026-12-06',
        latestAcceptableDate: null,
      },
    });
    http.match((r) => r.url.endsWith('/api/ref/fabric-responsibilities')).forEach((r) => r.flush([{ id: 1, name: 'Vendor Supplied' }]));
    fixture.detectChanges();

    const root = fixture.nativeElement as HTMLElement;
    expect(root.querySelector('h1')?.textContent).toContain('PO-2026-00001');
    expect(root.textContent).toContain('PKR 500');
    expect(root.textContent).toContain('spec.pdf');
    expect(root.textContent).toContain('A change is proposed and not yet agreed');
    expect(root.textContent).toContain('Vendor Supplied');
    expect(root.querySelector('nav')).toBeNull();
  });

  it('says the PO is not available for a missing or never-sent PO', () => {
    const { fixture, http } = create();
    http.expectOne((r) => r.url.endsWith('/vendor-view')).flush(null, { status: 404, statusText: 'Not Found' });
    http.match((r) => r.url.includes('/api/ref/')).forEach((r) => r.flush([]));
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).textContent).toContain('This purchase order is not available.');
  });
});
