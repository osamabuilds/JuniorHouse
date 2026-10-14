import { TestBed } from '@angular/core/testing';
import { binAge } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { BinAgeDto } from '../../models';
import { BinPickerComponent } from './bin-picker.component';

describe('BinPickerComponent (spec CC-1, CC-3, CC-11)', () => {
  function create(bins: BinAgeDto[], selected: number[] = [], loading = false) {
    const fixture = TestBed.createComponent(BinPickerComponent);
    fixture.componentRef.setInput('bins', bins);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('selected', selected);
    const toggled = vi.fn();
    fixture.componentInstance.toggled.subscribe(toggled);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, toggled };
  }

  it('says when there are no bins, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('There are no bins yet.');
    expect(textOf(create([], [], true).fixture)).toContain('Loading…');
  });

  it('says how long ago each bin was counted, in words (CC-11)', () => {
    const text = textOf(
      create([
        binAge({ binId: 1, binCode: 'A-01-1' }),
        binAge({ binId: 2, binCode: 'A-01-2', daysSinceCounted: 0 }),
        binAge({ binId: 3, binCode: 'A-01-3', daysSinceCounted: 1 }),
        binAge({ binId: 4, binCode: 'A-01-4', daysSinceCounted: 30 }),
      ]).fixture,
    );

    expect(text).toContain('(never counted)');
    expect(text).toContain('(counted today)');
    expect(text).toContain('(counted 1 day ago)');
    expect(text).toContain('(counted 30 days ago)');
  });

  it('a bin in an open count, or an inactive one, cannot be ticked, and says why (CC-3)', () => {
    const { root, fixture } = create([binAge({ binId: 1, inOpenCount: true }), binAge({ binId: 2, binCode: 'B', isActive: false }), binAge({ binId: 3, binCode: 'C' })]);

    const boxes = Array.from(root.querySelectorAll('input[type="checkbox"]')) as HTMLInputElement[];
    expect(boxes.map((b) => b.disabled)).toEqual([true, true, false]);
    expect(textOf(fixture)).toContain('(in an open count)');
    expect(textOf(fixture)).toContain('(inactive)');
  });

  it('ticking a bin emits its id, every checkbox is inside a label, and the group has a legend', () => {
    const { root, toggled } = create([binAge({ binId: 7, binCode: 'A-03-2' })], [7]);

    const box = root.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(box.checked).toBe(true);
    expect(box.closest('label')).not.toBeNull();
    expect(root.querySelector('legend')?.textContent).toContain('Choose the bins to count');
    box.click();
    expect(toggled).toHaveBeenCalledWith(7);
  });
});
