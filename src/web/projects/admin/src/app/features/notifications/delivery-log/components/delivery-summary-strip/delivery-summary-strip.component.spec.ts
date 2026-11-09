import { TestBed } from '@angular/core/testing';
import { DeliverySummary } from '../../models';
import { waitText } from '../../utils/delivery-labels.util';
import { DeliverySummaryStripComponent } from './delivery-summary-strip.component';

const summary: DeliverySummary = {
  countsByStatus: { QUED: 2, SENT: 10, FAIL: 3 },
  queuedCount: 2,
  deadLettersAwaitingResend: 1,
  oldestDueSeconds: 1200,
  whatsAppSentToday: 14,
};

describe('waitText (spec DLV-11)', () => {
  it.each([
    [0, 'Nothing waiting'],
    [45, '45 s'],
    [600, '10 min'],
    [3600, '1 h'],
    [7500, '2 h 5 min'],
  ])('%i seconds reads as %s', (seconds, expected) => {
    expect(waitText(seconds)).toBe(expected);
  });
});

describe('DeliverySummaryStripComponent (spec DLV-11, CHN-15)', () => {
  function create(data = summary) {
    const fixture = TestBed.createComponent(DeliverySummaryStripComponent);
    fixture.componentRef.setInput('summary', data);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('shows the queue, the oldest wait, the dead letters, the failures and the WhatsApp cost driver', () => {
    const root = create();
    const text = root.textContent ?? '';

    expect(text).toContain('Waiting to send');
    expect(text).toContain('20 min');
    expect(text).toContain('Gave up, not resent');
    expect(text).toContain('WhatsApp sent today');
    expect(text).toContain('14');
  });

  it('marks a wait of fifteen minutes or more, and any dead letter, as needing attention', () => {
    const root = create();

    expect(root.querySelectorAll('dd.is-late')).toHaveLength(2);
  });

  it('marks nothing when the queue is healthy', () => {
    const root = create({ ...summary, oldestDueSeconds: 0, deadLettersAwaitingResend: 0 });

    expect(root.querySelectorAll('dd.is-late')).toHaveLength(0);
  });
});
