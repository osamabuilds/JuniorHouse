import { TestBed } from '@angular/core/testing';
import { formatDate } from '@shared';
import { LOOKUP_NAMES_STUB } from '@app/testing/lookup-names.stub';
import { milestone, textOf } from '@app/testing/production-fixtures';
import { MilestoneTimelineComponent } from './milestone-timeline.component';

describe('MilestoneTimelineComponent (spec AC-5, AC-7, AC-36, AC-38)', () => {
  function create(milestones: ReturnType<typeof milestone>[]) {
    const fixture = TestBed.createComponent(MilestoneTimelineComponent);
    fixture.componentRef.setInput('milestones', milestones);
    fixture.componentRef.setInput('names', LOOKUP_NAMES_STUB);
    fixture.detectChanges();
    return fixture;
  }

  it('says nothing has been recorded yet', () => {
    expect(textOf(create([]))).toContain('Nothing recorded yet');
  });

  it('shows what happened, the day, who reported it and who recorded it', () => {
    const text = textOf(create([milestone({ typeId: 6, claimedDate: '2026-09-10', reportedBy: 'Ali Raza', actingPerson: 'Osama Shafique' })]));

    expect(text).toContain('Bulk cutting started');
    expect(text).toContain(formatDate('2026-09-10'));
    expect(text).toContain('reported by Ali Raza');
    expect(text).toContain('recorded by Osama Shafique');
  });

  it('marks a milestone recorded long after it happened, in words (AC-7)', () => {
    const text = textOf(create([milestone({ isRecordedLate: true })]));

    expect(text).toContain('Recorded late');
  });

  it('spells out a loud warning on bulk cutting (AC-36, AC-38)', () => {
    const text = textOf(create([milestone({ typeId: 6, flagReason: "Cutting started before Romp's fabric was dispatched." })]));

    expect(text).toContain('Warning:');
    expect(text).toContain("Cutting started before Romp's fabric was dispatched.");
  });

  it('shows finished quantities by size and colour', () => {
    const text = textOf(create([milestone({ typeId: 7, finishedLines: [{ sizeId: 1, colourId: 10, qty: 80 }] })]));

    expect(text).toContain('80 × Size 1 / Colour 10');
  });

  it('is a list with an item for each milestone (semantic markup)', () => {
    const fixture = create([milestone({ id: 1 }), milestone({ id: 2, typeId: 3 })]);

    expect((fixture.nativeElement as HTMLElement).querySelectorAll('ol > li').length).toBe(2);
  });
});
