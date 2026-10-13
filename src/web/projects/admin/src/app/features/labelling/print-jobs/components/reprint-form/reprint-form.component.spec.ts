import { TestBed } from '@angular/core/testing';
import { printer } from '@app/testing/labelling-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { ReprintValue } from '../../models';
import { ReprintFormComponent } from './reprint-form.component';

describe('ReprintFormComponent (spec PJ-5, PJ-7, PJ-12)', () => {
  function create(defaultPerson = 'Asha') {
    const fixture = TestBed.createComponent(ReprintFormComponent);
    fixture.componentRef.setInput('sku', 'ROMP-STY-DRESS-RED-2-3Y');
    fixture.componentRef.setInput('printers', [printer()]);
    fixture.componentRef.setInput('defaultPerson', defaultPerson);
    const submitted = vi.fn<(value: ReprintValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, submitted };
  }

  const submit = (root: HTMLElement) => (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
  const type = (root: HTMLElement, selector: string, value: string) => {
    const input = root.querySelector(selector) as HTMLInputElement;
    input.value = value;
    input.dispatchEvent(new Event('input'));
  };

  it('says the barcode does not change and the tag shows today\'s price', () => {
    const { fixture } = create();

    expect(textOf(fixture)).toContain('Reprint tags for ROMP-STY-DRESS-RED-2-3Y');
    expect(textOf(fixture)).toContain('The barcode number does not change');
    expect(textOf(fixture)).toContain("today's price");
  });

  it('offers the four reasons and the configured printers only', () => {
    const { root } = create();

    const reasons = Array.from(root.querySelectorAll('#rp-reason option')).map((o) => o.textContent?.trim());
    expect(reasons).toEqual(['Tag lost', 'Tag damaged', 'Price changed', 'Other']);
    expect(Array.from(root.querySelectorAll('#rp-printer option')).map((o) => (o as HTMLOptionElement).value)).toEqual(['', 'FAKE-PDF']);
  });

  it('submits the count, reason, trimmed note and name; an empty note and printer become null', () => {
    const { root, submitted } = create();
    type(root, '#rp-qty', '3');
    type(root, '#rp-note', '  ripped  ');

    submit(root);

    expect(submitted).toHaveBeenCalledWith({ qty: 3, reasonId: 1, note: 'ripped', printerCode: null, actingPerson: 'Asha' });

    type(root, '#rp-note', '   ');
    submit(root);
    expect(submitted).toHaveBeenLastCalledWith(expect.objectContaining({ note: null }));
  });

  it('refuses no name, zero tags and more than 5,000 tags (PJ-7)', () => {
    const { fixture, root, submitted } = create('');

    type(root, '#rp-qty', '2');
    submit(root); // no name
    expect(submitted).not.toHaveBeenCalled();
    fixture.detectChanges();
    expect(textOf(fixture)).toContain("This can't be queued yet");

    type(root, '#rp-person', 'Asha');
    type(root, '#rp-qty', '0');
    submit(root);
    expect(submitted).not.toHaveBeenCalled();

    type(root, '#rp-qty', '5001');
    submit(root);
    expect(submitted).not.toHaveBeenCalled();

    type(root, '#rp-qty', '5000');
    submit(root);
    expect(submitted).toHaveBeenCalledOnce();
  });

  it('shows the API reason (for example a voided GTIN) in an alert', () => {
    const { fixture } = create();
    fixture.componentRef.setInput('error', { status: 409, message: 'The GTIN for ROMP-X has been voided; tags for it can no longer be printed.', fieldErrors: {} });
    fixture.detectChanges();

    expect((fixture.nativeElement as HTMLElement).querySelector('[role="alert"]')?.textContent).toContain('voided');
  });
});
