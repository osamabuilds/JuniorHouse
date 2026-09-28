import { TestBed } from '@angular/core/testing';
import { PoDto } from './po-api.service';
import { SendConfirm } from './send-confirm';

const po = {
  poNo: 'PO-2026-00001',
  latestAcceptableDate: '2026-12-15',
  fabricResponsibilityId: 1,
  unitCost: 500,
  lines: [{ sizeId: 1, colourId: 10, qty: 5 }],
} as unknown as PoDto;

describe('SendConfirm (SCRUM-93 task 53, AC-39)', () => {
  function create(hasTechPack: boolean) {
    const fixture = TestBed.createComponent(SendConfirm);
    fixture.componentRef.setInput('po', po);
    fixture.componentRef.setInput('hasTechPack', hasTechPack);
    fixture.detectChanges();
    return fixture;
  }

  it('lets the PO be sent straight away when a tech pack is attached', () => {
    const root = create(true).nativeElement as HTMLElement;

    expect(root.querySelector('input[type="checkbox"]')).toBeNull();
    expect((root.querySelector('.button--primary') as HTMLButtonElement).disabled).toBe(false);
  });

  it('requires an explicit "send anyway" when no tech pack is attached', () => {
    const fixture = create(false);
    const root = fixture.nativeElement as HTMLElement;
    const confirmed = vi.fn();
    fixture.componentInstance.confirmed.subscribe(confirmed);
    const send = root.querySelector('.button--primary') as HTMLButtonElement;

    expect(root.textContent).toContain('No Tech Pack Spec is attached');
    expect(send.disabled).toBe(true);

    const anyway = root.querySelector('input[type="checkbox"]') as HTMLInputElement;
    anyway.checked = true;
    anyway.dispatchEvent(new Event('change'));
    fixture.detectChanges();
    send.click();

    expect(send.disabled).toBe(false);
    expect(confirmed).toHaveBeenCalledWith(true);
  });

  it('shows the checklist as a reminder, not a gate', () => {
    const fixture = create(true);
    fixture.componentRef.setInput('po', { ...po, fabricResponsibilityId: null });
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelectorAll('li').length).toBe(4);
    expect(root.textContent).toContain('Fabric responsibility set');
    expect((root.querySelector('.button--primary') as HTMLButtonElement).disabled).toBe(false);
  });
});
