import { TestBed } from '@angular/core/testing';
import { TrackingEvent, needsAttention } from '../../models';
import { TrackingTimelineComponent } from './tracking-timeline.component';

const events: TrackingEvent[] = [
  { status: 'PICKED_UP', statusName: 'Picked up by the courier', courierEventId: 'e1', occurredAt: '2026-11-09T10:00:00Z', source: 'PUSH' },
  { status: 'FAILED_ATTEMPT', statusName: 'Delivery attempt failed', courierEventId: 'e2', occurredAt: '2026-11-10T12:00:00Z', source: 'POLL' },
];

describe('TrackingTimelineComponent (spec TRK-13)', () => {
  function create(data: readonly TrackingEvent[]) {
    const fixture = TestBed.createComponent(TrackingTimelineComponent);
    fixture.componentRef.setInput('events', data);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('TRK-13 lists every status in order with its time and where it came from', () => {
    const root = create(events);
    const items = Array.from(root.querySelectorAll('li'));

    expect(items).toHaveLength(2);
    expect(items[0].textContent).toContain('Picked up by the courier');
    expect(items[0].textContent).toContain('Courier pushed it');
    expect(items[1].textContent).toContain('We asked the courier');
    expect(items[0].querySelector('time')?.getAttribute('datetime')).toBe('2026-11-09T10:00:00Z');
  });

  it('TRK-7 a failed attempt is marked in words, not by colour alone', () => {
    const root = create(events);

    expect(root.querySelectorAll('li')[1].textContent).toContain('needs attention');
    expect(root.querySelectorAll('li')[0].textContent).not.toContain('needs attention');
  });

  it('an empty timeline says the courier has reported nothing, in an ordered list otherwise', () => {
    expect(create([]).textContent).toContain('has not reported anything yet');
    expect(create(events).querySelector('ol')).not.toBeNull();
  });

  it('needsAttention names the statuses staff must look at', () => {
    expect(needsAttention('FAILED_ATTEMPT')).toBe(true);
    expect(needsAttention('PENDING_BOOKING')).toBe(true);
    expect(needsAttention('DELIVERED')).toBe(false);
  });
});
