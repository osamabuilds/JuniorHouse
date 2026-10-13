import { TestBed } from '@angular/core/testing';
import { textOf } from '@app/testing/production-fixtures';
import { ReconciliationBadgeComponent } from './reconciliation-badge.component';

describe('ReconciliationBadgeComponent (spec AC-7, AC-44)', () => {
  function create(code: 'Matched' | 'Short' | 'Over', diff = 0) {
    const fixture = TestBed.createComponent(ReconciliationBadgeComponent);
    fixture.componentRef.setInput('code', code);
    fixture.componentRef.setInput('diff', diff);
    fixture.detectChanges();
    return fixture;
  }

  it.each([
    ['Matched', 0, 'Matched the delivery note'],
    ['Short', -12, 'Short by 12'],
    ['Over', 5, 'Over by 5'],
  ] as const)('says %s in words, not only in colour', (code, diff, expected) => {
    expect(textOf(create(code, diff))).toContain(expected);
  });

  it('marks only a matched receipt as fine', () => {
    const matched = (create('Matched').nativeElement as HTMLElement).querySelector('.qc-flag');
    const short = (create('Short', -1).nativeElement as HTMLElement).querySelector('.qc-flag');

    expect(matched?.classList.contains('qc-flag--ok')).toBe(true);
    expect(short?.classList.contains('qc-flag--warn')).toBe(true);
  });
});
