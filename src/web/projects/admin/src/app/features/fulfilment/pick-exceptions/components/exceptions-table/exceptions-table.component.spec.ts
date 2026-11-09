import { TestBed } from '@angular/core/testing';
import { ExceptionRow } from '../../models';
import { ExceptionsTableComponent, ResolveRequest } from './exceptions-table.component';

const rows: ExceptionRow[] = [
  { id: 3, orderId: 5, orderNumber: 'RMP-5', sku: 'SKU-9', binCode: 'A-01-1', reason: 'SHORT', status: 'OPEN', systemCount: 3, foundCount: 1, raisedBy: 'picker', raisedAt: '2026-11-09T09:00:00Z', resolvedBy: null, resolvedAt: null },
  { id: 4, orderId: 6, orderNumber: 'RMP-6', sku: 'SKU-10', binCode: 'B-02-2', reason: 'DAMAGED', status: 'RESOLVED', systemCount: 1, foundCount: 0, raisedBy: 'picker', raisedAt: '2026-11-09T08:00:00Z', resolvedBy: 'manager', resolvedAt: '2026-11-09T10:00:00Z' },
];

describe('ExceptionsTableComponent (spec PEX-4)', () => {
  function create(canResolve: boolean) {
    const fixture = TestBed.createComponent(ExceptionsTableComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('canResolve', canResolve);
    fixture.detectChanges();
    const resolved: ResolveRequest[] = [];
    fixture.componentInstance.resolve.subscribe((r) => resolved.push(r));
    return { resolved, root: fixture.nativeElement as HTMLElement };
  }

  it('PEX-4 shows the order, SKU, bin, reason, system count, count found, picker and time', () => {
    const { root } = create(false);
    const first = root.querySelectorAll('tbody tr')[0].textContent ?? '';

    for (const text of ['RMP-5', 'SKU-9', 'A-01-1', 'Fewer than the system says', 'picker', 'Open']) {
      expect(first).toContain(text);
    }
    const cells = Array.from(root.querySelectorAll('tbody tr')[0].querySelectorAll('td')).map((c) => c.textContent?.trim());
    expect(cells).toContain('3');
    expect(cells).toContain('1');
  });

  it('PEX-8 an open exception offers both ways to close it, and a closed one says who closed it', () => {
    const { resolved, root } = create(true);
    const buttons = Array.from(root.querySelectorAll('tbody tr')[0].querySelectorAll('button')) as HTMLButtonElement[];

    expect(buttons.map((b) => b.textContent?.trim())).toEqual(['Stock is back', 'Drop the line']);
    buttons[0].click();
    buttons[1].click();

    expect(resolved).toEqual([{ id: 3, action: 'restocked' }, { id: 3, action: 'cancel-line' }]);
    expect(root.querySelectorAll('tbody tr')[1].textContent).toContain('Resolved by manager');
    expect(root.querySelectorAll('tbody tr')[1].querySelectorAll('button')).toHaveLength(0);
  });

  it('SEC-08 without the manage permission there is no resolve column at all', () => {
    const { root } = create(false);

    expect(root.querySelectorAll('button')).toHaveLength(0);
    expect(Array.from(root.querySelectorAll('thead th')).map((h) => h.textContent)).not.toContain('Resolve');
  });
});
