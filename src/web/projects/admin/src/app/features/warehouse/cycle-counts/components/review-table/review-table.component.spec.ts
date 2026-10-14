import { TestBed } from '@angular/core/testing';
import { REASONS, review, reviewLine } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { CountReviewDto, ReasonChoice, reviewKey } from '../../models';
import { ReviewTableComponent } from './review-table.component';

describe('ReviewTableComponent (spec CC-4, CC-6, CC-7, CC-8, ADJ-4b)', () => {
  function create(data: CountReviewDto, choices: Record<string, ReasonChoice> = {}, canConfirm = false) {
    const fixture = TestBed.createComponent(ReviewTableComponent);
    fixture.componentRef.setInput('review', data);
    fixture.componentRef.setInput('reasons', REASONS);
    fixture.componentRef.setInput('choices', choices);
    fixture.componentRef.setInput('canConfirm', canConfirm);
    const reasonChosen = vi.fn();
    const reconfirmToggled = vi.fn();
    const confirmed = vi.fn();
    const closed = vi.fn();
    fixture.componentInstance.reasonChosen.subscribe(reasonChosen);
    fixture.componentInstance.reconfirmToggled.subscribe(reconfirmToggled);
    fixture.componentInstance.confirmed.subscribe(confirmed);
    fixture.componentInstance.closed.subscribe(closed);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, reasonChosen, reconfirmToggled, confirmed, closed };
  }

  it('shows counted against system and the difference, with a sign (CC-4)', () => {
    const text = textOf(create(review([reviewLine(), reviewLine({ variantRefId: 32, skuCode: 'OTHER', countedQuantity: 5, systemQuantity: 0, variance: 5 })])).fixture);

    expect(text).toContain('Review CC-2026-0001');
    expect(text).toContain('-2');
    expect(text).toContain('+5');
  });

  it('a line with no difference needs no reason (CC-8), and one with a difference has a labelled reason list (CC-4)', () => {
    const { root, fixture } = create(review([reviewLine(), reviewLine({ variantRefId: 32, skuCode: 'SAME', countedQuantity: 4, systemQuantity: 4, variance: 0 })]));

    expect(textOf(fixture)).toContain('No difference');
    expect(root.querySelectorAll('select')).toHaveLength(1);
    expect(root.querySelector('label .sr-only')?.textContent).toContain('Reason for the difference on ROMP-STY-DRESS-RED-2-3Y in A-03-2');
    const options = Array.from(root.querySelectorAll('option')).map((o) => o.textContent?.trim());
    expect(options).toEqual(['Choose a reason…', 'Damage', 'Miscount', 'Theft', 'Found stock']);
  });

  it('choosing a reason emits the line key and the reason', () => {
    const { root, reasonChosen } = create(review());
    const select = root.querySelector('select') as HTMLSelectElement;

    select.value = '3';
    select.dispatchEvent(new Event('change'));

    expect(reasonChosen).toHaveBeenCalledWith({ key: reviewKey(reviewLine()), reasonId: 3 });
  });

  it('flags a line that changed since the count opened, and says to recount (CC-7)', () => {
    const { fixture } = create(review([reviewLine({ stale: true })]));

    expect(textOf(fixture)).toContain('Changed since the count opened');
    expect(textOf(fixture)).toContain('Count those bins again, ticking "recount"');
  });

  it('says when a bin has not been counted yet (CC-4) and notes items never scanned', () => {
    const { fixture } = create(review([reviewLine({ notCounted: true, countedQuantity: 0, variance: -10 })], { allBinsSubmitted: false }));

    expect(textOf(fixture)).toContain('Not every bin has been counted yet');
    expect(textOf(fixture)).toContain('Not scanned: counted as none');
  });

  it('a large difference asks for an explicit confirmation tick (ADJ-4b)', () => {
    const line = reviewLine({ variance: -30, countedQuantity: 70, systemQuantity: 100 });
    const { root, reconfirmToggled } = create(review([line]));

    const tick = root.querySelector('input[type="checkbox"]') as HTMLInputElement;
    expect(tick.parentElement?.textContent).toContain('I confirm this large change');
    tick.click();
    expect(reconfirmToggled).toHaveBeenCalledWith({ key: reviewKey(line), reconfirmed: true });
  });

  it('a small difference has no confirmation tick', () => {
    expect(create(review()).root.querySelector('input[type="checkbox"]')).toBeNull();
  });

  it('Confirm is disabled until the parent says every rule is met, and Back returns to counting', () => {
    const blocked = create(review(), {}, false);
    const confirmBlocked = Array.from(blocked.root.querySelectorAll('button')).find((b) => b.textContent?.includes('Confirm the count')) as HTMLButtonElement;
    expect(confirmBlocked.disabled).toBe(true);

    const ready = create(review(), {}, true);
    const confirm = Array.from(ready.root.querySelectorAll('button')).find((b) => b.textContent?.includes('Confirm the count')) as HTMLButtonElement;
    confirm.click();
    expect(ready.confirmed).toHaveBeenCalled();
    (Array.from(ready.root.querySelectorAll('button')).find((b) => b.textContent?.includes('Back to counting')) as HTMLButtonElement).click();
    expect(ready.closed).toHaveBeenCalled();
  });
});
