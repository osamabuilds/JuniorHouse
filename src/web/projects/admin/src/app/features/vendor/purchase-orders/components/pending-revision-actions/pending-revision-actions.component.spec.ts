import { TestBed } from '@angular/core/testing';
import { PendingRevisionActionsComponent } from './pending-revision-actions.component';
import { PoRevisionDto } from '../../models';

const pending = (initiatorId: number): PoRevisionDto =>
  ({ id: 2, poId: 10, revisionNumber: 1, initiatorId, statusId: 1, unitCost: 600, expectedDeliveryDate: '2026-12-06' }) as unknown as PoRevisionDto;

describe('PendingRevisionActionsComponent (SCRUM-93 task 50)', () => {
  function create(initiatorId: number) {
    const fixture = TestBed.createComponent(PendingRevisionActionsComponent);
    fixture.componentRef.setInput('revision', pending(initiatorId));
    fixture.detectChanges();
    return fixture;
  }

  const textOf = (fixture: { nativeElement: unknown }): string => ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');

  it('names the vendor as decider for a buyer-proposed revision', () => {
    const text = textOf(create(1));

    expect(text).toContain('Proposed by Romp buyer');
    expect(text).toContain("decision is Vendor's");
  });

  it('names Romp as decider for a vendor-proposed revision', () => {
    const text = textOf(create(2));

    expect(text).toContain('Proposed by Vendor');
    expect(text).toContain("decision is Romp buyer's");
  });

  it('emits accept, and reject/withdraw with the note', () => {
    const fixture = create(1);
    const root = fixture.nativeElement as HTMLElement;
    const accepted = vi.fn();
    const rejected = vi.fn();
    const withdrawn = vi.fn();
    fixture.componentInstance.accepted.subscribe(accepted);
    fixture.componentInstance.rejected.subscribe(rejected);
    fixture.componentInstance.withdrawn.subscribe(withdrawn);

    const note = root.querySelector('input') as HTMLInputElement;
    note.value = 'too high';
    note.dispatchEvent(new Event('input'));
    const buttons = Array.from(root.querySelectorAll('button'));
    buttons[0].click();
    buttons[1].click();
    buttons[2].click();

    expect(accepted).toHaveBeenCalledOnce();
    expect(rejected).toHaveBeenCalledWith('too high');
    expect(withdrawn).toHaveBeenCalledWith('too high');
  });
});
