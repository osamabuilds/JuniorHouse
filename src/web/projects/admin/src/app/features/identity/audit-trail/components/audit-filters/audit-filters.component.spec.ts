import { TestBed } from '@angular/core/testing';
import { controlsWithoutLabels, typeInto } from '@app/testing/dom-helpers';
import { AuditFormValue, EMPTY_AUDIT_FORM } from '../../models';
import { AuditFiltersComponent } from './audit-filters.component';

describe('AuditFiltersComponent (audit-trail AC-8, AC-9)', () => {
  function create() {
    const fixture = TestBed.createComponent(AuditFiltersComponent);
    const applied = vi.fn<(value: AuditFormValue) => void>();
    fixture.componentInstance.applied.subscribe(applied);
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, applied };
  }

  it('gives every control a label', () => {
    expect(controlsWithoutLabels(create().root)).toEqual([]);
  });

  it('reports what was typed, trimmed, with codes in capitals', () => {
    const { root, applied } = create();
    typeInto(root, '#af-actor', ' amna.khan ');
    typeInto(root, '#af-module', 'ctlg');
    typeInto(root, '#af-type', 'styl_main');
    typeInto(root, '#af-key', '42');
    typeInto(root, '#af-from', '2026-10-01');

    root.querySelector('form')?.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(applied).toHaveBeenCalledWith({ ...EMPTY_AUDIT_FORM, actor: 'amna.khan', module: 'CTLG', entityType: 'STYL_MAIN', entityKey: '42', fromDate: '2026-10-01' });
  });

  it('clears back to the empty form', () => {
    const { root, applied } = create();

    root.querySelector('form')?.dispatchEvent(new Event('reset', { cancelable: true }));

    expect(applied).toHaveBeenCalledWith(EMPTY_AUDIT_FORM);
  });
});
