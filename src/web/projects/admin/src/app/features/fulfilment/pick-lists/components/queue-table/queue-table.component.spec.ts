import { TestBed } from '@angular/core/testing';
import { QueueItem } from '../../models';
import { QueueTableComponent } from './queue-table.component';

const rows: QueueItem[] = [
  { orderId: 1, orderNumber: 'RMP-1', status: 'QUEUED', queuedAt: '2026-11-09T08:00:00Z', ageMinutes: 125, lineCount: 2, unitCount: 3, claimedBy: null, putBackRequired: false, isCashOnDelivery: true },
  { orderId: 2, orderNumber: 'RMP-2', status: 'PICKING', queuedAt: '2026-11-09T09:00:00Z', ageMinutes: 5, lineCount: 1, unitCount: 1, claimedBy: 'picker two', putBackRequired: false, isCashOnDelivery: false },
];

describe('QueueTableComponent (spec APP-8, PCK-11, APP-5)', () => {
  function create(me: string | null = 'picker one') {
    const fixture = TestBed.createComponent(QueueTableComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('me', me);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('APP-8 lists orders in the order given, each with how long it has waited', () => {
    const { root } = create();
    const bodyRows = Array.from(root.querySelectorAll('tbody tr'));

    expect(bodyRows).toHaveLength(2);
    expect(bodyRows[0].textContent).toContain('RMP-1');
    expect(bodyRows[0].textContent).toContain('2 h 5 min');
    expect(bodyRows[1].textContent).toContain('5 min');
  });

  it('PCK-11 shows the order number and the payment kind, and no customer', () => {
    const { root } = create();
    const text = root.textContent ?? '';

    expect(text).toContain('Cash on delivery');
    expect(text).toContain('Paid');
    expect(text).not.toMatch(/phone|email|address/i);
  });

  it('NFR-19 is a real table with a caption and column headers', () => {
    const { root } = create();

    expect(root.querySelector('caption')).not.toBeNull();
    expect(root.querySelectorAll('thead th[scope="col"]').length).toBeGreaterThanOrEqual(5);
    expect(root.querySelectorAll('tbody th[scope="row"]')).toHaveLength(2);
  });

  it('PCK-9 an order someone else holds cannot be opened, and says who holds it', () => {
    const { root } = create();
    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];

    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
    expect(root.textContent).toContain('picker two');
  });

  it('the button names the order for a screen reader and reports the click', () => {
    const { fixture, root } = create();
    const opened: number[] = [];
    fixture.componentInstance.open.subscribe((id) => opened.push(id));

    (root.querySelector('button') as HTMLButtonElement).click();

    expect(root.querySelector('button .sr-only')?.textContent).toContain('RMP-1');
    expect(opened).toEqual([1]);
  });
});
