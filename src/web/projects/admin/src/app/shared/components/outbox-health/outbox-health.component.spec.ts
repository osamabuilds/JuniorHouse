import { TestBed } from '@angular/core/testing';
import { OutboxSchemaHealth } from '@core/health';
import { OutboxHealthComponent } from './outbox-health.component';

const schema = (overrides: Partial<OutboxSchemaHealth> = {}): OutboxSchemaHealth => ({
  schema: 'VNDR',
  pending: 0,
  inFlight: 0,
  retrying: 0,
  deadLettered: 0,
  oldestPendingSeconds: null,
  ...overrides,
});

describe('OutboxHealthComponent (SCRUM-186)', () => {
  function create(health: readonly OutboxSchemaHealth[] | null) {
    const fixture = TestBed.createComponent(OutboxHealthComponent);
    fixture.componentRef.setInput('health', health);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { root, text: (root.textContent ?? '').replace(/\s+/g, ' ').trim(), problem: root.querySelector('.outbox--problem') !== null };
  }

  it('shows nothing until the API has answered', () => {
    expect(create(null).text).toBe('');
  });

  it('says everything was delivered when nothing is waiting', () => {
    const { text, problem } = create([schema(), schema({ schema: 'PROD' })]);

    expect(text).toContain('all delivered');
    expect(problem).toBe(false);
  });

  it('counts what is on its way and being retried across every module, without raising an alarm', () => {
    const { text, problem } = create([schema({ pending: 2, inFlight: 1 }), schema({ schema: 'PROD', retrying: 3, oldestPendingSeconds: 20 })]);

    expect(text).toContain('3 on their way, 3 being retried');
    expect(problem).toBe(false);
  });

  it('warns in words when something has been waiting for minutes', () => {
    const { text, problem } = create([schema({ pending: 4, oldestPendingSeconds: 600 })]);

    expect(text).toContain('4 waiting for more than 10 minutes');
    expect(problem).toBe(true);
  });

  it('warns in words when an event could not be delivered', () => {
    const { text, problem } = create([schema({ deadLettered: 2 })]);

    expect(text).toContain('2 could not be delivered');
    expect(problem).toBe(true);
  });

  it('announces itself politely to screen readers', () => {
    expect(create([schema()]).root.querySelector('[role="status"]')).not.toBeNull();
  });
});
