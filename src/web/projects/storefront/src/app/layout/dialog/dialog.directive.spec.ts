import { ChangeDetectionStrategy, Component, viewChild } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RpDialog } from './dialog.directive';

@Component({
  imports: [RpDialog],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <button id="opener" type="button">open</button>
    <dialog rpDialog aria-label="Test dialog"><button id="inside" type="button">inside</button></dialog>
  `,
})
class Host {
  readonly dialog = viewChild.required(RpDialog);
}

describe('RpDialog', () => {
  beforeAll(() => {
    // jsdom has no <dialog> modal behaviour; model the parts the directive relies on.
    const proto = HTMLDialogElement.prototype as unknown as Record<string, unknown>;
    proto['showModal'] = function (this: HTMLDialogElement) { this.setAttribute('open', ''); };
    proto['close'] = function (this: HTMLDialogElement) { this.removeAttribute('open'); this.dispatchEvent(new Event('close')); };
  });

  function setup() {
    const fixture = TestBed.createComponent(Host);
    fixture.detectChanges();
    const el: HTMLDialogElement = fixture.nativeElement.querySelector('dialog');
    const opener: HTMLButtonElement = fixture.nativeElement.querySelector('#opener');
    opener.focus();
    return { fixture, el, opener, ctl: fixture.componentInstance.dialog() };
  }

  it('SC-8 opens modally and locks body scroll', () => {
    const { el, ctl } = setup();
    ctl.open();
    expect(el.hasAttribute('open')).toBe(true);
    expect(document.body.classList.contains('rp-no-scroll')).toBe(true);
    ctl.close();
  });

  it('SC-8 closes on Escape (cancel), unlocks scroll and returns focus to the opener', () => {
    const { el, opener, ctl } = setup();
    ctl.open();
    el.querySelector<HTMLElement>('#inside')!.focus();
    el.dispatchEvent(new Event('cancel', { cancelable: true }));
    expect(el.hasAttribute('open')).toBe(false);
    expect(document.body.classList.contains('rp-no-scroll')).toBe(false);
    expect(document.activeElement).toBe(opener);
  });

  it('SC-8 closes when the scrim (the dialog element itself) is clicked, not its content', () => {
    const { el, ctl } = setup();
    ctl.open();
    el.querySelector<HTMLElement>('#inside')!.click();
    expect(el.hasAttribute('open')).toBe(true);
    el.click();
    expect(el.hasAttribute('open')).toBe(false);
  });

  it('SC-8 keeps the scroll lock until the last open dialog closes', () => {
    const a = setup();
    const b = setup();
    a.ctl.open();
    b.ctl.open();
    a.ctl.close();
    expect(document.body.classList.contains('rp-no-scroll')).toBe(true);
    b.ctl.close();
    expect(document.body.classList.contains('rp-no-scroll')).toBe(false);
  });
});
