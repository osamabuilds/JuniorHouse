import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { inboundSummary } from '@app/testing/quality-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { InboundTableComponent } from './inbound-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('InboundTableComponent (spec AC-1, AC-5, AC-44)', () => {
  function create(deliveries: ReturnType<typeof inboundSummary>[], loading = false) {
    TestBed.configureTestingModule({ providers: [provideAllowAllPermissions(), provideRouter([])] });
    const fixture = TestBed.createComponent(InboundTableComponent);
    fixture.componentRef.setInput('deliveries', deliveries);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    return fixture;
  }

  it('says what to expect when there is nothing to receive', () => {
    expect(textOf(create([]))).toContain('A delivery note appears in this queue');
  });

  it('shows a loading message while the queue loads', () => {
    expect(textOf(create([], true))).toContain('Loading');
  });

  it('offers to receive a delivery that has not been counted, linking to its own page', () => {
    const fixture = create([inboundSummary({ id: 11, vendorDnNo: 'DN-1', claimedTotal: 150 })]);
    const root = fixture.nativeElement as HTMLElement;

    expect(textOf(fixture)).toContain('Waiting to be received');
    expect(textOf(fixture)).toContain('150 pieces');
    expect(root.querySelector('a[href="/quality/receive/11"]')?.textContent).toContain('Receive goods');
  });

  it('shows a received delivery with its GRN number and no receive link', () => {
    const fixture = create([inboundSummary({ grnId: 31, grnNo: 'GRN-2026-00001' })]);

    expect(textOf(fixture)).toContain('Received: GRN-2026-00001');
    expect((fixture.nativeElement as HTMLElement).querySelector('a[href^="/quality/receive"]')).toBeNull();
  });

  it('says a cancelled purchase order cannot be received, and marks the final shipment', () => {
    const fixture = create([inboundSummary({ isCancelled: true, isFinalShipment: true })]);

    expect(textOf(fixture)).toContain('Purchase order cancelled');
    expect(textOf(fixture)).toContain('Final shipment');
    expect((fixture.nativeElement as HTMLElement).querySelector('a[href^="/quality/receive"]')).toBeNull();
  });
});
