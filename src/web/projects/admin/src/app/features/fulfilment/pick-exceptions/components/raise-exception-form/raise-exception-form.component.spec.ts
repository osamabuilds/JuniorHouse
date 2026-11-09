import { TestBed } from '@angular/core/testing';
import { RaiseExceptionRequest } from '../../models';
import { FlaggableLine, RaiseExceptionFormComponent } from './raise-exception-form.component';

const lines: FlaggableLine[] = [
  { key: '9@A-01-1', variantId: 9, binCode: 'A-01-1', label: 'Floral Dress, Red, 2-3Y at A-01-1', remaining: 3 },
  { key: '10@B-02-2', variantId: 10, binCode: 'B-02-2', label: 'Denim Jacket, Blue, 4-5Y at B-02-2', remaining: 1 },
];

describe('RaiseExceptionFormComponent (spec PEX-1, PEX-5, PEX-4)', () => {
  function create() {
    const fixture = TestBed.createComponent(RaiseExceptionFormComponent);
    fixture.componentRef.setInput('lines', lines);
    fixture.detectChanges();
    const raised: RaiseExceptionRequest[] = [];
    fixture.componentInstance.raised.subscribe((r) => raised.push(r));
    return { fixture, raised, root: fixture.nativeElement as HTMLElement };
  }

  function choose(root: HTMLElement, id: string, value: string): void {
    const element = root.querySelector(`#${id}`) as HTMLSelectElement | HTMLInputElement;
    element.value = value;
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? 'change' : 'input'));
  }

  function submit(fixture: { detectChanges: () => void }, root: HTMLElement): void {
    root.querySelector('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    fixture.detectChanges();
  }

  it('PEX-5 a reason is required: nothing is sent and the form says so', () => {
    const { fixture, raised, root } = create();

    submit(fixture, root);

    expect(raised).toEqual([]);
    expect(root.textContent).toContain('Choose a reason');
  });

  it('PEX-5 the found count is a whole number from 0 to the units still to take', () => {
    const { fixture, raised, root } = create();
    choose(root, 'raise-reason', 'SHORT');
    choose(root, 'raise-found', '4');
    fixture.detectChanges();

    submit(fixture, root);

    expect(raised).toEqual([]);
    expect(root.textContent).toContain('0 to 3');
  });

  it('PEX-1 a valid form reports the line, the bin, the reason and the count', () => {
    const { fixture, raised, root } = create();
    choose(root, 'raise-reason', 'NOT_FOUND');
    choose(root, 'raise-found', '1');
    fixture.detectChanges();

    submit(fixture, root);

    expect(raised).toEqual([{ variantId: 9, binCode: 'A-01-1', reason: 'NOT_FOUND', foundCount: 1 }]);
  });

  it('PEX-1 another line can be chosen, and its own limit applies', () => {
    const { fixture, raised, root } = create();
    choose(root, 'raise-line', '10@B-02-2');
    choose(root, 'raise-reason', 'DAMAGED');
    choose(root, 'raise-found', '2');
    fixture.detectChanges();

    submit(fixture, root);
    expect(raised).toEqual([]);

    choose(root, 'raise-found', '0');
    fixture.detectChanges();
    submit(fixture, root);
    expect(raised).toEqual([{ variantId: 10, binCode: 'B-02-2', reason: 'DAMAGED', foundCount: 0 }]);
  });

  it('NFR-19 every field has a label', () => {
    const { root } = create();

    for (const id of ['raise-line', 'raise-reason', 'raise-found']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }
  });
});
