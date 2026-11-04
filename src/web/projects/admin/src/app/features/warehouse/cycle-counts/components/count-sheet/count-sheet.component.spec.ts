import { TestBed } from '@angular/core/testing';
import { countDto } from '@app/testing/warehouse-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { CycleCountDto, SubmitLinesValue } from '../../models';
import { CountSheetComponent } from './count-sheet.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('CountSheetComponent (spec CC-2, CC-6, CC-7, CC-10)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(count: CycleCountDto = countDto()) {
    const fixture = TestBed.createComponent(CountSheetComponent);
    fixture.componentRef.setInput('count', count);
    fixture.componentRef.setInput('defaultPerson', 'Ayesha Khan');
    const submitted = vi.fn<(value: SubmitLinesValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const type = (id: string, value: string) => {
      const input = root.querySelector(`#${id}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    const pickBin = (code: string) => {
      const select = root.querySelector('#cs-bin') as HTMLSelectElement;
      select.value = code;
      select.dispatchEvent(new Event('change'));
    };
    const submit = () => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    return { fixture, root, submitted, type, pickBin, submit };
  }

  it('never shows a system quantity: the blind count has nothing to show (CC-2)', () => {
    const text = textOf(create().fixture).toLowerCase();

    expect(text).not.toContain('system');
    expect(text).not.toContain('expected');
    expect(text).not.toContain('difference');
    expect(text).not.toContain('variance');
  });

  it('lists each bin as counted or not yet counted, and what has been counted so far', () => {
    const text = textOf(create().fixture);

    expect(text).toContain('A-03-2: counted');
    expect(text).toContain('B-01-1: not counted yet');
    expect(text).toContain('ROMP-STY-DRESS-RED-2-3Y');
  });

  it('every field has a visible label, and the remembered name is filled in (WCAG, NFR-19)', () => {
    const { root } = create();

    for (const id of ['cs-bin', 'cs-person', 'cs-gtin-0', 'cs-grade-0', 'cs-qty-0']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }

    expect((root.querySelector('#cs-person') as HTMLInputElement).value).toBe('Ayesha Khan');
  });

  it('sends the bin with its counted lines and the recount flag (CC-6, CC-7)', () => {
    const { root, submitted, type, pickBin, submit } = create();
    pickBin('B-01-1');
    type('cs-gtin-0', '2000000000015');
    type('cs-qty-0', '4');
    (root.querySelector('input[type="checkbox"]') as HTMLInputElement).click();

    submit();

    expect(submitted).toHaveBeenCalledWith({
      binCode: 'B-01-1',
      lines: [{ gtin: '2000000000015', gradeId: 1, quantity: 4 }],
      recount: true,
      actingPerson: 'Ayesha Khan',
    });
  });

  it('a bin with nothing in it is counted by sending no lines (CC-8)', () => {
    const { root, submitted, pickBin, submit } = create();
    pickBin('B-01-1');
    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Remove this line')) as HTMLButtonElement).click();

    submit();

    expect(submitted).toHaveBeenCalledWith(expect.objectContaining({ binCode: 'B-01-1', lines: [] }));
  });

  it('refuses a short barcode, a negative or half quantity, and no bin before anything is sent (CC-10)', () => {
    const { submitted, type, pickBin, submit, fixture } = create();

    submit();
    pickBin('B-01-1');
    type('cs-gtin-0', '123');
    submit();
    type('cs-gtin-0', '2000000000015');
    type('cs-qty-0', '-1');
    submit();
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
  });

  it('a closed count shows what was counted but offers no way to count more', () => {
    const { root } = create(countDto({ statusCode: 'CLOSED' }));

    expect(root.querySelector('form')).toBeNull();
    expect(textOf(create(countDto({ statusCode: 'CLOSED' })).fixture)).toContain('Counted so far');
  });
});
