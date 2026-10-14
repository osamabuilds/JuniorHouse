import { TestBed } from '@angular/core/testing';
import { adjustmentValue, REASONS } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { PendingReconfirm, PostAdjustmentValue } from '../../models';
import { AdjustmentFormComponent } from './adjustment-form.component';

describe('AdjustmentFormComponent (spec ADJ-1, ADJ-3, ADJ-4b, ADJ-7, ADJ-8)', () => {
  function create(reconfirm: PendingReconfirm | null = null) {
    const fixture = TestBed.createComponent(AdjustmentFormComponent);
    fixture.componentRef.setInput('reasons', REASONS);
    fixture.componentRef.setInput('reconfirm', reconfirm);
    fixture.componentRef.setInput('defaultPerson', 'Ayesha Khan');
    const submitted = vi.fn<(value: PostAdjustmentValue) => void>();
    const confirmed = vi.fn<(value: PostAdjustmentValue) => void>();
    const declined = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.componentInstance.confirmed.subscribe(confirmed);
    fixture.componentInstance.declined.subscribe(declined);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const type = (id: string, value: string) => {
      const input = root.querySelector(`#${id}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const choose = (id: string, value: string) => {
      const select = root.querySelector(`#${id}`) as HTMLSelectElement;
      select.value = Array.from(select.options).find((o) => o.textContent?.trim().startsWith(value))!.value;
      select.dispatchEvent(new Event('change'));
    };
    const submit = () => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    return { fixture, root, submitted, confirmed, declined, type, choose, submit };
  }

  function fill(h: ReturnType<typeof create>, change: string) {
    h.type('af-gtin', '2000000000015');
    h.type('af-bin', 'a-03-2');
    h.choose('af-reason', 'Damage');
    h.type('af-change', change);
  }

  it('every field has a visible label (WCAG, NFR-19), the reason is a list of the four codes, and the name is remembered', () => {
    const { root } = create();

    for (const id of ['af-gtin', 'af-bin', 'af-grade', 'af-reason', 'af-change', 'af-person', 'af-note']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }

    const reasons = Array.from((root.querySelector('#af-reason') as HTMLSelectElement).options).map((o) => o.textContent?.trim());
    expect(reasons.filter((r) => r?.includes('('))).toHaveLength(4);
    expect(reasons.join(' ')).toContain('Damage (removes units)');
    expect(reasons.join(' ')).toContain('Found stock (adds units)');
    expect((root.querySelector('#af-person') as HTMLInputElement).value).toBe('Ayesha Khan');
  });

  it('sends the change with the reason, upper-cased bin and a stable idempotency key, not yet reconfirmed (ADJ-0, ADJ-8)', () => {
    const h = create();
    fill(h, '-3');

    h.submit();

    expect(h.submitted).toHaveBeenCalledOnce();
    const sent = h.submitted.mock.calls[0][0];
    expect(sent).toMatchObject({ gtin: '2000000000015', binCode: 'A-03-2', quantityChange: -3, reasonId: 1, reconfirmed: false, actingPerson: 'Ayesha Khan' });
    expect(sent.idempotencyKey).toMatch(/^[0-9a-f-]{36}$/);

    h.submit();
    expect(h.submitted.mock.calls[1][0].idempotencyKey).toBe(sent.idempotencyKey);
  });

  it('refuses no reason, a zero change, a half unit and a short barcode before anything is sent (ADJ-1, ADJ-7)', () => {
    const h = create();
    h.type('af-gtin', '123');
    h.type('af-bin', 'A-03-2');
    h.type('af-change', '0');
    h.submit();
    h.choose('af-reason', 'Damage');
    h.type('af-gtin', '2000000000015');
    h.type('af-change', '0');
    h.submit();
    h.type('af-change', '1.5');
    h.submit();
    h.fixture.detectChanges();

    expect(h.submitted).not.toHaveBeenCalled();
    expect(textOf(h.fixture)).toContain("This can't be saved yet");
  });

  it('above the limit it shows a confirmation step; confirming resends the same attempt, going back leaves the form alone (ADJ-4b)', () => {
    const pending: PendingReconfirm = { value: adjustmentValue(), message: 'Confirmation required: changing 30 units is above 20. Confirm it to continue.' };
    const { root, confirmed, declined, fixture } = create(pending);

    const dialog = root.querySelector('[role="alertdialog"]') as HTMLElement;
    expect(dialog.textContent).toContain('Confirmation required: changing 30 units is above 20.');
    expect(dialog.getAttribute('aria-labelledby')).toBe('af-confirm-heading');

    const buttons = Array.from(dialog.querySelectorAll('button')) as HTMLButtonElement[];
    buttons.find((b) => b.textContent?.includes('Yes, apply'))!.click();
    expect(confirmed).toHaveBeenCalledWith(pending.value);
    buttons.find((b) => b.textContent?.includes('Go back'))!.click();
    expect(declined).toHaveBeenCalled();
    expect(textOf(fixture)).toContain('Please confirm this change');
  });

  it('without a pending question there is no confirmation step', () => {
    expect(create().root.querySelector('[role="alertdialog"]')).toBeNull();
  });
});
