import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FieldComponent } from './field.component';

@Component({
  imports: [FieldComponent],
  template: `
    <rp-field #f label="Mobile number" hint="For delivery updates" [error]="err()">
      <input
        class="rp-input"
        [id]="f.controlId"
        [attr.aria-describedby]="f.describedBy()"
        [attr.aria-invalid]="f.invalid()"
      />
    </rp-field>
  `,
})
class HostComponent {
  err = signal('');
}

describe('FieldComponent', () => {
  function setup() {
    const f = TestBed.createComponent(HostComponent);
    f.detectChanges();
    return f;
  }

  it('DSF-7 puts the label above the input and ties it to the control', () => {
    const el: HTMLElement = setup().nativeElement;
    const label = el.querySelector('label')!;
    const input = el.querySelector('input')!;
    expect(label.getAttribute('for')).toBe(input.id);
    expect(label.compareDocumentPosition(input) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(input.getAttribute('aria-invalid')).toBeNull();
  });

  it('DSF-7 links the error text and marks the input invalid', () => {
    const f = setup();
    f.componentInstance.err.set('Enter 11 digits starting with 03');
    f.detectChanges();
    const el: HTMLElement = f.nativeElement;
    const input = el.querySelector('input')!;
    const error = el.querySelector('.rp-error')!;
    expect(input.getAttribute('aria-invalid')).toBe('true');
    expect(input.getAttribute('aria-describedby')).toContain(error.id);
    expect(error.textContent).toContain('Enter 11 digits starting with 03');
  });
});
