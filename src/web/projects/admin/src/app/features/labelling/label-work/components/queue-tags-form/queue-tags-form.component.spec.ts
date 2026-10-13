import { TestBed } from '@angular/core/testing';
import { printer, workItem } from '@app/testing/labelling-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { QueueTagsValue } from '../../models';
import { QueueTagsFormComponent } from './queue-tags-form.component';

describe('QueueTagsFormComponent (spec PJ-1, PJ-7, PJ-12)', () => {
  function create(remainingQty = 10, defaultPerson = 'Asha') {
    const fixture = TestBed.createComponent(QueueTagsFormComponent);
    fixture.componentRef.setInput('item', workItem({ remainingQty }));
    fixture.componentRef.setInput('printers', [printer(), printer({ id: 2, code: 'FAKE-JAM', name: 'Jammed printer (always fails)' })]);
    fixture.componentRef.setInput('defaultPerson', defaultPerson);
    const submitted = vi.fn<(value: QueueTagsValue) => void>();
    const cancelled = vi.fn();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.componentInstance.cancelled.subscribe(cancelled);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted, cancelled };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

  it('starts with every remaining tag and the remembered name, and names the SKU', () => {
    const { fixture, root } = create(24, 'Asha');

    expect((root.querySelector('#qt-qty') as HTMLInputElement).value).toBe('24');
    expect((root.querySelector('#qt-person') as HTMLInputElement).value).toBe('Asha');
    expect(textOf(fixture)).toContain('Queue tags for ROMP-STY-DRESS-RED-2-3Y');
    expect(textOf(fixture)).toContain('24 more tags are allowed');
  });

  it('offers only the configured printers, by name, with the default first (PJ-12)', () => {
    const { root } = create();

    const options = Array.from(root.querySelectorAll('#qt-printer option')) as HTMLOptionElement[];
    expect(options.map((o) => o.textContent?.trim())).toEqual(['Default printer', 'Preview printer (no hardware)', 'Jammed printer (always fails)']);
    expect(options.map((o) => o.value)).toEqual(['', 'FAKE-PDF', 'FAKE-JAM']);
  });

  it('submits the count, the chosen printer code and the trimmed name', () => {
    const { fixture, root, submitted } = create(10, '  Asha  ');
    const qty = root.querySelector('#qt-qty') as HTMLInputElement;
    qty.value = '6';
    qty.dispatchEvent(new Event('input'));
    const select = root.querySelector('#qt-printer') as HTMLSelectElement;
    select.value = 'FAKE-JAM';
    select.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    submit(root);

    expect(submitted).toHaveBeenCalledWith({ qty: 6, printerCode: 'FAKE-JAM', actingPerson: 'Asha' });
  });

  it('refuses a count above what is allowed, below one, or no name, and says why', () => {
    const { fixture, root, submitted } = create(5, '');
    const qty = root.querySelector('#qt-qty') as HTMLInputElement;

    qty.value = '6';
    qty.dispatchEvent(new Event('input'));
    submit(root);
    fixture.detectChanges();
    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be queued yet");

    qty.value = '0';
    qty.dispatchEvent(new Event('input'));
    submit(root);
    expect(submitted).not.toHaveBeenCalled();

    qty.value = '3';
    qty.dispatchEvent(new Event('input'));
    submit(root); // still no name
    expect(submitted).not.toHaveBeenCalled();
  });

  it('shows the API reason next to the field, and can be cancelled', () => {
    const { fixture, root, cancelled } = create();
    fixture.componentRef.setInput('error', { status: 400, message: 'Only 4 more tags are allowed for this item.', fieldErrors: { Qty: ['Only 4 more tags are allowed for this item.'] } });
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('Only 4 more tags are allowed for this item.');
    (Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes('Cancel')) as HTMLButtonElement).click();
    expect(cancelled).toHaveBeenCalledOnce();
  });

  it('disables the button while saving so a double click cannot queue twice', () => {
    const { fixture, root } = create();
    fixture.componentRef.setInput('saving', true);
    fixture.detectChanges();

    const button = root.querySelector('button[type="submit"]') as HTMLButtonElement;
    expect(button.disabled).toBe(true);
    expect(button.textContent).toContain('Queueing…');
  });
});
