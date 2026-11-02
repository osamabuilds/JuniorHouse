import { TestBed } from '@angular/core/testing';
import { CodAnswer, CodLinkStatus, CodOutcome } from '../../models/cod.model';
import { CodResponseComponent } from './cod-response.component';

const open: CodLinkStatus = { isValid: true, orderNumber: 'RMP-2026-00001-4', canRespond: true, statusCode: 'PNDC' };

function render(inputs: { link?: CodLinkStatus | null; loading?: boolean; outcome?: CodOutcome | null; failed?: boolean; sending?: boolean }) {
  TestBed.resetTestingModule();
  const fixture = TestBed.createComponent(CodResponseComponent);
  for (const [name, value] of Object.entries(inputs)) fixture.componentRef.setInput(name, value);
  fixture.detectChanges();
  const answers: CodAnswer[] = [];
  fixture.componentInstance.answer.subscribe((a) => answers.push(a));
  return { el: fixture.nativeElement as HTMLElement, answers };
}

const button = (el: HTMLElement, name: string) => [...el.querySelectorAll('button')].find((b) => b.textContent?.trim() === name) as HTMLButtonElement | undefined;

describe('CodResponseComponent', () => {
  it('COD-12 shows Confirm my order and Cancel my order for a pending order, and sends nothing until pressed', () => {
    const { el, answers } = render({ link: open });

    expect(button(el, 'Confirm my order')).toBeDefined();
    expect(button(el, 'Cancel my order')).toBeDefined();
    expect(answers).toEqual([]);
  });

  it('COD-12 pressing Confirm my order asks to confirm, Cancel my order asks to cancel', () => {
    const { el, answers } = render({ link: open });

    button(el, 'Confirm my order')!.click();
    button(el, 'Cancel my order')!.click();

    expect(answers).toEqual(['confirm', 'cancel']);
  });

  it('CHK-10 the buttons wait while an answer is on its way', () => {
    const { el } = render({ link: open, sending: true });

    expect(button(el, 'Confirm my order')!.disabled).toBe(true);
    expect(button(el, 'Cancel my order')!.disabled).toBe(true);
  });

  it('COD-19 an order already handled gets a neutral message and no buttons', () => {
    const { el } = render({ link: { ...open, canRespond: false, statusCode: 'CNFD' } });

    expect(el.textContent).toContain('already been dealt with');
    expect(el.querySelectorAll('button')).toHaveLength(0);
  });

  it('COD-19 a link that is not valid says so in words and shows nothing about any order', () => {
    const { el } = render({ link: { isValid: false, orderNumber: null, canRespond: false, statusCode: null } });

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('could not use this link');
    expect(el.querySelectorAll('button')).toHaveLength(0);
  });

  it('after a confirmation it thanks the customer; after a cancellation it says nothing is owed', () => {
    expect(render({ link: open, outcome: 'Confirmed' }).el.textContent).toContain('your order is confirmed');
    expect(render({ link: open, outcome: 'Cancelled' }).el.textContent).toContain('Nothing is owed');
  });

  it('a failed send says nothing changed and keeps the buttons', () => {
    const { el } = render({ link: open, failed: true });

    expect(el.querySelector('[role="alert"]')?.textContent).toContain('Nothing has changed');
    expect(button(el, 'Confirm my order')).toBeDefined();
  });
});
