import { TestBed } from '@angular/core/testing';
import { binRow } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { BinRowDto } from '../../models';
import { BinTableComponent } from './bin-table.component';

describe('BinTableComponent (spec BIN-4, BIN-6)', () => {
  function create(bins: BinRowDto[], loading = false, togglingId: number | null = null) {
    const fixture = TestBed.createComponent(BinTableComponent);
    fixture.componentRef.setInput('bins', bins);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('togglingId', togglingId);
    const toggle = vi.fn();
    fixture.componentInstance.toggle.subscribe(toggle);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, toggle };
  }

  it('says when there are no bins, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No bins found.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('is a real table with column headers, a caption and the code, units and last-counted state of each bin (BIN-6)', () => {
    const { root, fixture } = create([binRow({ unitsOnHand: 7 }), binRow({ id: 6, binCode: 'B-01-1', lastCountedAt: '2026-10-01T08:00:00Z' })]);

    expect(root.querySelector('caption')?.textContent).toContain('Bins');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
    const text = textOf(fixture);
    expect(text).toContain('A-03-2');
    expect(text).toContain('Never');
    expect(text).toContain('1 Oct 2026');
  });

  it('a bin that holds stock cannot be switched off, and the button says why (BIN-4)', () => {
    const { root } = create([binRow({ unitsOnHand: 4 })]);

    const button = root.querySelector('button') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.getAttribute('title')).toContain('holds stock');
  });

  it('an empty bin can be switched off, and an inactive one can be switched back on', () => {
    const { root, toggle } = create([binRow({ id: 1 }), binRow({ id: 2, binCode: 'B-01-1', isActive: false, unitsOnHand: 0 })]);

    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons[0].textContent).toContain('Deactivate');
    expect(buttons[1].textContent).toContain('Activate');
    buttons[0].click();
    expect(toggle).toHaveBeenCalledWith(expect.objectContaining({ id: 1 }));
  });

  it('only the bin being saved is disabled', () => {
    const { root } = create([binRow({ id: 1 }), binRow({ id: 2, binCode: 'B-01-1' })], false, 2);

    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
  });
});
