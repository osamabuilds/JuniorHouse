import { TestBed } from '@angular/core/testing';
import { ApiError } from '@core/http';
import { textOf } from '@app/testing/production-fixtures';
import { CreateBinValue } from '../../models';
import { BinFormComponent } from './bin-form.component';

describe('BinFormComponent (spec BIN-1, BIN-3, BIN-7)', () => {
  function create(error: ApiError | null = null) {
    const fixture = TestBed.createComponent(BinFormComponent);
    fixture.componentRef.setInput('error', error);
    fixture.componentRef.setInput('defaultPerson', 'Ayesha Khan');
    const submitted = vi.fn<(value: CreateBinValue) => void>();
    fixture.componentInstance.submitted.subscribe(submitted);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const type = (id: string, value: string) => {
      const input = root.querySelector(`#${id}`) as HTMLInputElement;
      input.value = value;
      input.dispatchEvent(new Event('input'));
    };
    return { fixture, root, submitted, type };
  }

  it('every field has a visible label, and the remembered name is filled in (WCAG, NFR-19)', () => {
    const { root } = create();

    for (const id of ['bf-zone', 'bf-aisle', 'bf-shelf', 'bf-person']) {
      expect(root.querySelector(`label[for="${id}"]`)).not.toBeNull();
    }

    expect((root.querySelector('#bf-person') as HTMLInputElement).value).toBe('Ayesha Khan');
  });

  it('sends the parts upper-cased and trimmed so the server builds the printed code (BIN-1)', () => {
    const { root, submitted, type } = create();
    type('bf-zone', 'a');
    type('bf-aisle', '03');
    type('bf-shelf', 'b');

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));

    expect(submitted).toHaveBeenCalledWith({ zone: 'A', aisle: '03', shelf: 'B', actingPerson: 'Ayesha Khan' });
  });

  it('refuses a part outside the pattern before anything is sent (BIN-3)', () => {
    const { root, submitted, type, fixture } = create();
    type('bf-zone', 'A-1');
    type('bf-aisle', 'xx');
    type('bf-shelf', '');

    (root.querySelector('form') as HTMLFormElement).dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(submitted).not.toHaveBeenCalled();
    expect(textOf(fixture)).toContain("This can't be saved yet");
  });

  it('shows the server\'s reason for a field under that field (BIN-2)', () => {
    const { fixture } = create({ status: 400, message: 'The request has invalid details.', fieldErrors: { Zone: ['The zone must be 1 to 3 capital letters or digits.'] } });

    expect(textOf(fixture)).toContain('The zone must be 1 to 3 capital letters or digits.');
  });
});
