import { TestBed } from '@angular/core/testing';
import { adjustment } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { AdjustmentDto } from '../../models';
import { AdjustmentTableComponent } from './adjustment-table.component';

describe('AdjustmentTableComponent (spec ADJ-0, ADJ-6, ADJ-10)', () => {
  function create(adjustments: AdjustmentDto[], loading = false) {
    const fixture = TestBed.createComponent(AdjustmentTableComponent);
    fixture.componentRef.setInput('adjustments', adjustments);
    fixture.componentRef.setInput('loading', loading);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement };
  }

  it('says when there are no adjustments, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No adjustments found.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows what changed, the balance before and after, why, the note, and who did it (ADJ-0)', () => {
    const text = textOf(create([adjustment(), adjustment({ id: 81, quantityChange: 5, reasonCode: 'FOUND_STOCK', note: null, balanceBefore: 7, balanceAfter: 12, actorName: 'Bilal Ahmed' })]).fixture);

    expect(text).toContain('-3');
    expect(text).toContain('10 → 7');
    expect(text).toContain('Damage');
    expect(text).toContain('crushed in the aisle');
    expect(text).toContain('+5');
    expect(text).toContain('Found stock');
    expect(text).toContain('Bilal Ahmed');
  });

  it('marks a large change that was confirmed', () => {
    expect(textOf(create([adjustment({ reconfirmed: true })]).fixture)).toContain('Large change, confirmed');
  });

  it('has no edit or delete control at all: an adjustment is a record (ADJ-6)', () => {
    const { root } = create([adjustment()]);

    expect(root.querySelectorAll('button, a, input, select')).toHaveLength(0);
    expect(root.querySelector('caption')?.textContent).toContain('Stock adjustments');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
