import { TestBed } from '@angular/core/testing';
import { textOf } from '@app/testing/production-fixtures';
import { PutawayScanComponent } from './putaway-scan.component';

describe('PutawayScanComponent (spec PUT-14, PUT-6, decision D7: keyboard-wedge scanners)', () => {
  function create(inputs: { scanning?: boolean; message?: string | null } = {}) {
    const fixture = TestBed.createComponent(PutawayScanComponent);
    fixture.componentRef.setInput('scanning', inputs.scanning ?? false);
    fixture.componentRef.setInput('message', inputs.message ?? null);
    const scanned = vi.fn<(gtin: string) => void>();
    fixture.componentInstance.scanned.subscribe(scanned);
    document.body.appendChild(fixture.nativeElement as HTMLElement);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    return { fixture, root, scanned, box: root.querySelector('input') as HTMLInputElement };
  }

  afterEach(() => document.body.replaceChildren());

  it('the barcode box has a label for screen readers and takes focus on its own, so a scanner can type straight away', async () => {
    const { fixture, root, box } = create();
    await fixture.whenStable();

    expect(root.querySelector('label')?.textContent).toContain('Barcode (13 digits)');
    expect(document.activeElement).toBe(box);
  });

  it('a scanner typing the number and pressing Enter (submitting the form) looks the barcode up', () => {
    const { root, box, scanned } = create();

    box.value = '2000000000015';
    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));

    expect(scanned).toHaveBeenCalledWith('2000000000015');
  });

  it('an empty box looks nothing up', () => {
    const { root, scanned } = create();

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit', { cancelable: true }));

    expect(scanned).not.toHaveBeenCalled();
  });

  it('takes focus again when the focus token changes (after a putaway, ready for the next tag)', async () => {
    const { fixture, box } = create();
    await fixture.whenStable();
    box.blur();
    expect(document.activeElement).not.toBe(box);

    fixture.componentRef.setInput('focusToken', 1);
    fixture.detectChanges();
    await fixture.whenStable();

    expect(document.activeElement).toBe(box);
  });

  it('shows the message as an alert tied to the box, and disables the button while looking up', () => {
    const { fixture, root, box } = create({ message: 'That barcode is not known to the warehouse.', scanning: true });

    expect(root.querySelector('[role="alert"]')?.textContent).toContain('not known');
    expect(box.getAttribute('aria-invalid')).toBe('true');
    expect(box.getAttribute('aria-describedby')).toBe('ps-message');
    expect((root.querySelector('button') as HTMLButtonElement).disabled).toBe(true);
    expect(textOf(fixture)).toContain('Looking up…');
  });
});
