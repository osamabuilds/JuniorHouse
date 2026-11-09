import { TestBed } from '@angular/core/testing';
import { ScanBoxComponent } from './scan-box.component';

describe('ScanBoxComponent (spec APP-3, APP-5, NFR-SC-07)', () => {
  async function create(busy = false) {
    const fixture = TestBed.createComponent(ScanBoxComponent);
    fixture.componentRef.setInput('busy', busy);
    document.body.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    await fixture.whenStable();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, input: root.querySelector('input') as HTMLInputElement, form: root.querySelector('form') as HTMLFormElement };
  }

  function type(input: HTMLInputElement, value: string): void {
    input.value = value;
    input.dispatchEvent(new Event('input'));
  }

  it('APP-3 the field has the focus when the screen opens, so a scanner works with no clicking', async () => {
    const { input } = await create();

    expect(document.activeElement).toBe(input);
  });

  it('APP-3 the field keeps the focus after a scan and is empty again for the next one', async () => {
    const { fixture, input, form } = await create();
    const codes: string[] = [];
    fixture.componentInstance.scanned.subscribe((c) => codes.push(c));
    (document.querySelector('body') as HTMLElement).focus();

    type(input, '  A-01-1 ');
    form.dispatchEvent(new Event('submit', { cancelable: true }));
    fixture.detectChanges();

    expect(codes).toEqual(['A-01-1']);
    expect(input.value).toBe('');
    expect(document.activeElement).toBe(input);
  });

  it('an empty scan sends nothing', async () => {
    const { fixture, input, form } = await create();
    const codes: string[] = [];
    fixture.componentInstance.scanned.subscribe((c) => codes.push(c));

    type(input, '   ');
    form.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(codes).toEqual([]);
  });

  it('while a scan is on its way the field is read-only and a second code is not sent', async () => {
    const { fixture, input, form, root } = await create(true);
    const codes: string[] = [];
    fixture.componentInstance.scanned.subscribe((c) => codes.push(c));

    type(input, '2000000000017');
    form.dispatchEvent(new Event('submit', { cancelable: true }));

    expect(input.readOnly).toBe(true);
    expect(codes).toEqual([]);
    expect((root.querySelector('button') as HTMLButtonElement).disabled).toBe(true);
  });

  it('NFR-19 the field has a visible label and the right input hints for a scanner', async () => {
    const { input, root } = await create();

    const label = root.querySelector('label') as HTMLLabelElement;
    expect(label.htmlFor).toBe(input.id);
    expect(label.textContent).toContain('Scan');
    expect(input.getAttribute('autocomplete')).toBe('off');
    expect(input.maxLength).toBe(40);
  });
});
