import { TestBed } from '@angular/core/testing';
import { variantRow } from '@app/testing/labelling-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { VoidGtinValue } from '../../models';
import { VoidFormComponent } from './void-form.component';

describe('VoidFormComponent (spec VAR-8)', () => {
  function create(defaultPerson = 'Asha') {
    const fixture = TestBed.createComponent(VoidFormComponent);
    fixture.componentRef.setInput('variant', variantRow());
    fixture.componentRef.setInput('defaultPerson', defaultPerson);
    const submitted = vi.fn<(value: VoidGtinValue) => void>();
    const cancelled = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.componentInstance.cancelled.subscribe(cancelled);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted, cancelled };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('explains the consequences before anyone presses the button', () => {
    const text = textOf(create().fixture);

    expect(text).toContain('Void the barcode for ROMP-STY-DRESS-RED-2-3Y');
    expect(text).toContain('0200000000019');
    expect(text).toContain('never used again');
    expect(text).toContain('Tags already on garments are not changed');
  });

  it('offers the three reasons', () => {
    const { root } = create();

    expect(Array.from(root.querySelectorAll('#vf-reason option')).map((o) => o.textContent?.trim())).toEqual(['Assigned in error', 'Product recalled', 'Other']);
  });

  it('submits the reason, a trimmed note (null when empty) and the name', () => {
    const { root, submitted } = create();
    const note = root.querySelector('#vf-note') as HTMLInputElement;
    note.value = '  wrong colour  ';
    note.dispatchEvent(new Event('input'));

    submit(root);

    expect(submitted).toHaveBeenCalledWith({ reasonId: 1, note: 'wrong colour', actingPerson: 'Asha' });
  });

  it('will not void without a name, and says why', () => {
    const { fixture, root, submitted } = create('');

    submit(root);
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
  });

  it('can be cancelled, and disables the button while saving', () => {
    const { fixture, root, cancelled } = create();
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();

    expect((root.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(true);
    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Cancel')) as HTMLButtonElement).click();
    expect(cancelled).toHaveBeenCalledOnce();
  });
});
