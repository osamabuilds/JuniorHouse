import { TestBed } from '@angular/core/testing';
import { ShippingLabel, barRuns } from '../../models';
import { LabelSheetComponent } from './label-sheet.component';

const label: ShippingLabel = {
  shipmentId: 3, orderNumber: 'RMP-5', courierName: 'Primary courier', waybill: 'PRM-000001-1234', barcodeModules: '1101001000011', recipientName: 'Sara Khan',
  phone: '03001234567', addressLine1: 'House 4, Street 9', addressLine2: 'Near the park', city: 'Lahore', province: 'Punjab', postalCode: '54000', deliveryNote: 'Call first',
  isCashOnDelivery: true, cashToCollect: 4700, weightGrams: 600, isReprint: false, printCount: 0,
};

describe('LabelSheetComponent (spec LBL-1, LBL-3, LBL-4, LBL-5, LBL-6)', () => {
  function create(data: ShippingLabel = label) {
    const fixture = TestBed.createComponent(LabelSheetComponent);
    fixture.componentRef.setInput('label', data);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('LBL-1 shows recipient, full address, phone, order, courier and waybill', () => {
    const text = create().textContent ?? '';

    for (const part of ['Sara Khan', 'House 4, Street 9', 'Near the park', 'Lahore', 'Punjab', '03001234567', 'RMP-5', 'Primary courier', 'PRM-000001-1234']) {
      expect(text).toContain(part);
    }
  });

  it('LBL-3 draws the waybill barcode as bars, readable by a screen reader as text', () => {
    const root = create();
    const svg = root.querySelector('svg') as SVGElement;

    expect(svg.getAttribute('aria-label')).toBe('Barcode of PRM-000001-1234');
    expect(svg.querySelectorAll('rect[fill="#000"]')).toHaveLength(barRuns(label.barcodeModules).length);
  });

  it('LBL-3 turns modules into runs of bars with their start and width', () => {
    expect(barRuns('1101001')).toEqual([{ x: 0, width: 2 }, { x: 3, width: 1 }, { x: 6, width: 1 }]);
    expect(barRuns('000')).toEqual([]);
  });

  it('LBL-4 a cash on delivery label prints the amount large, a paid one says Paid', () => {
    const cod = create();
    const paid = create({ ...label, isCashOnDelivery: false, cashToCollect: 0 });

    expect(cod.querySelector('.label__pay--cod')?.textContent).toContain('PKR 4,700');
    expect(paid.querySelector('.label__pay--cod')).toBeNull();
    expect(paid.textContent).toContain('Paid');
  });

  it('LBL-5 a reprint is marked', () => {
    expect(create({ ...label, isReprint: true, printCount: 2 }).querySelector('.label__reprint')?.textContent).toContain('2 times');
    expect(create().querySelector('.label__reprint')).toBeNull();
  });

  it('LBL-6 is a semantic article with no heading of its own', () => {
    const root = create();

    expect(root.querySelector('article')).not.toBeNull();
    expect(root.querySelectorAll('h1, h2, h3')).toHaveLength(0);
  });
});
