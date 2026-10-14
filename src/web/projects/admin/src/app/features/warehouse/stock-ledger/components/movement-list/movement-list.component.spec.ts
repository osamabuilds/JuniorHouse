import { TestBed } from '@angular/core/testing';
import { movement, stockRow } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { StockMovementDto } from '../../models';
import { MovementListComponent } from './movement-list.component';

describe('MovementListComponent (spec LED-9, NFR-SC-05)', () => {
  function create(movements: StockMovementDto[], loading = false) {
    const fixture = TestBed.createComponent(MovementListComponent);
    fixture.componentRef.setInput('variant', stockRow());
    fixture.componentRef.setInput('movements', movements);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('total', movements.length);
    const closed = vi.fn();
    fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, closed };
  }

  it('says when there are no movements, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No movements yet.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows what changed, where, the balance before and after, and who did it', () => {
    const text = textOf(create([movement(), movement({ id: 901, kindCode: 'ADJUSTMENT', quantityChange: -3, balanceBefore: 10, balanceAfter: 7, actorName: 'Bilal Ahmed' })]).fixture);

    expect(text).toContain('Movements for ROMP-STY-DRESS-RED-2-3Y');
    expect(text).toContain('Putaway');
    expect(text).toContain('+10');
    expect(text).toContain('0 → 10');
    expect(text).toContain('Adjustment');
    expect(text).toContain('-3');
    expect(text).toContain('10 → 7');
    expect(text).toContain('Bilal Ahmed');
  });

  it('is a region labelled by its heading and offers no way to edit a movement', () => {
    const { root, closed } = create([movement()]);

    const section = root.querySelector('section') as HTMLElement;
    expect(section.getAttribute('aria-labelledby')).toBe('ml-heading');
    expect(root.querySelector('#ml-heading')).not.toBeNull();
    expect(root.querySelectorAll('input, select, textarea')).toHaveLength(0);
    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Close')) as HTMLButtonElement).click();
    expect(closed).toHaveBeenCalled();
  });
});
