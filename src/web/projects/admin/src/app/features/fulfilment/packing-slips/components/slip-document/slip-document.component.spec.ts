import { TestBed } from '@angular/core/testing';
import { PackingSlip } from '../../models';
import { SlipDocumentComponent } from './slip-document.component';

const slip: PackingSlip = {
  orderNumber: 'RMP-5', placedAt: '2026-11-09T09:00:00Z', recipientName: 'Sara Khan', city: 'Lahore',
  lines: [
    { sku: 'SKU-9', name: 'Floral Dress', colour: 'Red', size: '2-3Y', quantity: 2, unitPrice: 1500, lineTotal: 3000 },
    { sku: 'SKU-10', name: 'Denim Jacket', colour: 'Blue', size: '4-5Y', quantity: 1, unitPrice: 2500, lineTotal: 2500 },
  ],
  subtotal: 5500, discount: 0, shipping: 200, total: 5700, isCashOnDelivery: true, cashToCollect: 5700, isPaid: false, brandName: 'Romp', exchangeWindowDays: 14,
  supportWhatsapp: '923001234567', supportEmail: 'hello@romp.pk', supportHours: 'Mon to Sat, 10am to 6pm', isReprint: false, printCount: 0, printPending: false,
};

describe('SlipDocumentComponent (spec SLP-1 to SLP-5, SLP-7, SLP-8)', () => {
  function create(data: PackingSlip = slip) {
    const fixture = TestBed.createComponent(SlipDocumentComponent);
    fixture.componentRef.setInput('slip', data);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('SLP-1 lists the order number, each line with SKU, size, colour, quantity and price, and the totals', () => {
    const root = create();
    const text = root.textContent ?? '';

    expect(text).toContain('RMP-5');
    expect(text).toContain('SKU-9');
    expect(text).toContain('2-3Y');
    expect(text).toContain('Red');
    expect(text).toContain('PKR 1,500');
    expect(text).toContain('PKR 3,000');
    expect(text).toContain('PKR 5,500');
    expect(text).toContain('PKR 5,700');
    expect(root.querySelectorAll('tbody tr')).toHaveLength(2);
  });

  it('SLP-2 a cash on delivery slip prints the amount to collect large', () => {
    const root = create();
    const cash = root.querySelector('.slip__pay--cod');

    expect(cash?.textContent).toContain('Cash to collect');
    expect(cash?.querySelector('.slip__pay-amount')?.textContent).toContain('PKR 5,700');
    expect(root.querySelector('.slip__pay--paid')).toBeNull();
  });

  it('SLP-3 an online slip says Paid and shows no amount to collect', () => {
    const root = create({ ...slip, isCashOnDelivery: false, isPaid: true, cashToCollect: 0 });

    expect(root.querySelector('.slip__pay--paid')?.textContent).toContain('Paid');
    expect(root.querySelector('.slip__pay--cod')).toBeNull();
    expect(root.textContent).not.toContain('Cash to collect');
  });

  it('SLP-4 the exchange window and the support contact come from the settings in the slip', () => {
    const root = create({ ...slip, exchangeWindowDays: 21, supportEmail: 'care@romp.pk', supportWhatsapp: '923009998877' });
    const text = root.textContent ?? '';

    expect(text).toContain('within 21 days');
    expect(text).toContain('+923009998877');
    expect(text).toContain('care@romp.pk');
  });

  it('SLP-5 a reprint is marked with how many times it was printed', () => {
    const root = create({ ...slip, isReprint: true, printCount: 2 });

    expect(root.querySelector('.slip__reprint')?.textContent).toContain('Reprint');
    expect(root.querySelector('.slip__reprint')?.textContent).toContain('2 times');
    expect(create().querySelector('.slip__reprint')).toBeNull();
  });

  it('SLP-7 shows the recipient name and city and nothing else about them', () => {
    const text = create().textContent ?? '';

    expect(text).toContain('Sara Khan, Lahore');
    expect(text).not.toMatch(/03\d{9}|@example|street|house/i);
  });

  it('SLP-8 is a semantic article with a captioned table, scoped headers and no heading above level two', () => {
    const root = create();

    expect(root.querySelector('article')).not.toBeNull();
    expect(root.querySelector('caption')).not.toBeNull();
    expect(root.querySelectorAll('h1')).toHaveLength(0);
    expect(root.querySelectorAll('h2')).toHaveLength(1);
    expect(root.querySelectorAll('thead th[scope="col"]').length).toBeGreaterThanOrEqual(6);
  });
});
