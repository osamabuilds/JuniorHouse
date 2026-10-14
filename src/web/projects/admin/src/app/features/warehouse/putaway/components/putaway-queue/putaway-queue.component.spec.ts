import { TestBed } from '@angular/core/testing';
import { putawayItem } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { PutawayItemRowDto } from '../../models';
import { PutawayQueueComponent } from './putaway-queue.component';

describe('PutawayQueueComponent (spec PUT-14, PUT-19, PUT-7)', () => {
  function create(items: PutawayItemRowDto[], loading = false) {
    const fixture = TestBed.createComponent(PutawayQueueComponent);
    fixture.componentRef.setInput('items', items);
    fixture.componentRef.setInput('loading', loading);
    const chosen = vi.fn();
    fixture.componentInstance.chosen.subscribe(chosen);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, chosen };
  }

  it('says when nothing is waiting, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('Nothing is waiting to be put away.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows "put away n of N" with what is left, and the grade (PUT-14)', () => {
    const text = textOf(create([putawayItem(), putawayItem({ id: 13, gradeCode: 'CONCESSION' })]).fixture);

    expect(text).toContain('4 of 10');
    expect(text).toContain('6 to go');
    expect(text).toContain('Concession');
    expect(text).toContain('Ready to put away');
  });

  it('choosing a ready item emits it, which is the way in when there is no tag to scan (PUT-19)', () => {
    const { root, chosen } = create([putawayItem()]);

    (root.querySelector('button') as HTMLButtonElement).click();

    expect(chosen).toHaveBeenCalledWith(expect.objectContaining({ id: 12 }));
  });

  it('an item still waiting for its barcode cannot be chosen, and says so', () => {
    const { root, fixture } = create([putawayItem({ skuCode: null, gtin: null, statusCode: 'AWAITING_GTIN' })]);

    expect((root.querySelector('button') as HTMLButtonElement).disabled).toBe(true);
    expect(textOf(fixture)).toContain('No barcode yet');
    expect(textOf(fixture)).toContain('Waiting for its barcode');
  });

  it('an item whose barcode was retired cannot be chosen, and says so (PUT-7)', () => {
    const { root, fixture } = create([putawayItem({ gtinRetired: true })]);

    expect((root.querySelector('button') as HTMLButtonElement).disabled).toBe(true);
    expect(textOf(fixture)).toContain('Barcode retired: cannot be put away');
  });

  it('is a real table with column headers and a caption', () => {
    const { root } = create([putawayItem()]);

    expect(root.querySelector('caption')?.textContent).toContain('Items waiting to be put away');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
