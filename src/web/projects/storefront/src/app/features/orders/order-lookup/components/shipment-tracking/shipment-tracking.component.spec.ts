import { TestBed } from '@angular/core/testing';
import { ShipmentTracking } from '../../models/shipment-tracking.model';
import { ShipmentTrackingComponent } from './shipment-tracking.component';

const tracking: ShipmentTracking = {
  courierName: 'Primary courier',
  waybill: 'WB-100',
  statusCode: 'IN_TRANSIT',
  statusName: 'On its way',
  steps: [
    { statusCode: 'BOOKED', statusName: 'Booked', at: '2026-11-09T10:00:00Z' },
    { statusCode: 'IN_TRANSIT', statusName: 'On its way', at: '2026-11-10T08:00:00Z' },
  ],
};

function render(value: ShipmentTracking) {
  const fixture = TestBed.createComponent(ShipmentTrackingComponent);
  fixture.componentRef.setInput('tracking', value);
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('ShipmentTrackingComponent (OHS-1, OHS-3, TRK-9)', () => {
  it('OHS-1 shows the courier, the waybill and the status', () => {
    const el = render(tracking);

    expect(el.textContent).toContain('Primary courier');
    expect(el.textContent).toContain('WB-100');
    expect(el.querySelector('b')?.textContent).toBe('On its way');
  });

  it('OHS-1 lists the timeline newest first as an ordered list', () => {
    const items = Array.from(render(tracking).querySelectorAll('ol > li')).map((li) => li.querySelector('span')?.textContent);

    expect(items).toEqual(['On its way', 'Booked']);
  });

  it('a failed attempt is said in words', () => {
    const el = render({ ...tracking, statusCode: 'FAILED_ATTEMPT', statusName: 'Delivery attempt failed' });

    expect(el.textContent).toContain('could not deliver it');
  });
});
