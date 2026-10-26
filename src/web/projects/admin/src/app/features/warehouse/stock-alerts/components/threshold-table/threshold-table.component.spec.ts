import { TestBed } from '@angular/core/testing';
import { thresholdRow } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { ThresholdRowDto } from '../../models';
import { ThresholdChange, ThresholdTableComponent } from './threshold-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('ThresholdTableComponent (spec ALR-6)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(rows: ThresholdRowDto[], loading = false, savingVariantId: number | null = null) {
    const fixture = TestBed.createComponent(ThresholdTableComponent);
    fixture.componentRef.setInput('rows', rows);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('savingVariantId', savingVariantId);
    const save = vi.fn<(change: ThresholdChange) => void>();
    fixture.componentInstance.save.subscribe(save);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const box = () => root.querySelector('input') as HTMLInputElement;
    const type = (value: string) => {
      box().value = value;
      box().dispatchEvent(new Event('input'));
      fixture.detectChanges();
    };
    return { fixture, root, save, box, type };
  }

  it('says when no variant has held stock, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No variants have held stock yet.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('each threshold box has a label naming its SKU, and says when the default applies', () => {
    const { root, fixture } = create([thresholdRow(), thresholdRow({ variantRefId: 32, skuCode: 'ROMP-OTHER', isOverride: true, effectiveThreshold: 8 })]);

    const labels = Array.from(root.querySelectorAll('label')).map((l) => l.textContent?.trim());
    expect(labels).toContain('Low-stock threshold for ROMP-STY-DRESS-RED-2-3Y');
    expect(labels).toContain('Low-stock threshold for ROMP-OTHER');
    expect(textOf(fixture)).toContain('Using the default');
    expect((root.querySelectorAll('input')[1] as HTMLInputElement).value).toBe('8');
  });

  it('saving sends the variant and the number typed (a threshold of 0 is allowed: alert only at stock-out)', () => {
    const { root, save, type } = create([thresholdRow()]);

    type('0');
    (root.querySelector('button') as HTMLButtonElement).click();

    expect(save).toHaveBeenCalledWith({ variantRefId: 31, quantity: 0 });
  });

  it('refuses a negative, fractional, empty or oversized number before anything is sent', () => {
    const { root, save, type, box } = create([thresholdRow()]);

    for (const bad of ['-1', '2.5', '', '100001']) {
      type(bad);
      expect((root.querySelector('button') as HTMLButtonElement).disabled).toBe(true);
      expect(box().getAttribute('aria-invalid')).toBe('true');
      expect(root.querySelector('[role="alert"]')?.textContent).toContain('whole number from 0 to 100000');
    }

    expect(save).not.toHaveBeenCalled();
  });

  it('only the row being saved is disabled', () => {
    const { root } = create([thresholdRow({ variantRefId: 1 }), thresholdRow({ variantRefId: 2, skuCode: 'B' })], false, 2);

    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];
    expect(buttons[0].disabled).toBe(false);
    expect(buttons[1].disabled).toBe(true);
    expect(buttons[1].textContent).toContain('Saving…');
  });
});
