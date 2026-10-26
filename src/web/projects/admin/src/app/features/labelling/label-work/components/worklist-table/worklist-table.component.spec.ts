import { TestBed } from '@angular/core/testing';
import { textOf } from '@app/testing/production-fixtures';
import { workItem } from '@app/testing/labelling-fixtures';
import { LabelWorkItemDto } from '../../models';
import { WorklistTableComponent } from './worklist-table.component';
import { provideAllowAllPermissions } from '@app/testing/provide-allow-all-permissions';

describe('WorklistTableComponent (spec LW-4, LW-5, LW-7, PJ-1)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideAllowAllPermissions()] }));
  function create(items: LabelWorkItemDto[], loading = false, resolvingId: number | null = null) {
    const fixture = TestBed.createComponent(WorklistTableComponent);
    fixture.componentRef.setInput('items', items);
    fixture.componentRef.setInput('loading', loading);
    fixture.componentRef.setInput('resolvingId', resolvingId);
    fixture.componentRef.setInput('previewUrlFor', (id: number) => `http://api/x/${id}/tag-preview`);
    const queue = vi.fn();
    const resolve = vi.fn();
    const confirmRetired = vi.fn();
    fixture.componentInstance.queue.subscribe(queue);
    fixture.componentInstance.resolve.subscribe(resolve);
    fixture.componentInstance.confirmRetired.subscribe(confirmRetired);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, queue, resolve, confirmRetired };
  }

  it('says when there is nothing to do, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('Nothing is waiting for tags.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows the SKU, barcode, style, colour and size, quantities and the ready status', () => {
    const text = textOf(create([workItem({ printedQty: 10, activeQty: 14, remainingQty: 10 })]).fixture);

    expect(text).toContain('ROMP-STY-DRESS-RED-2-3Y');
    expect(text).toContain('0200000000019');
    expect(text).toContain('Floral Summer Dress');
    expect(text).toContain('Red / 2-3 Years');
    expect(text).toContain('10 printed');
    expect(text).toContain('4 queued');
    expect(text).toContain('10 still allowed');
    expect(text).toContain('Ready to print');
  });

  it('marks concession stock with the 2ND mark so staff know what the tag will carry', () => {
    expect(textOf(create([workItem({ outcomeCode: 'Concession' })]).fixture)).toContain('Concession (2ND)');
  });

  it('flags a price that changed since the tags were queued, in words and not colour alone (LW-5)', () => {
    const text = textOf(create([workItem({ priceChanged: true })]).fixture);

    expect(text).toContain('Price changed since these tags were queued');
    expect(text).toContain('Reprint');
  });

  it('a parked item says what it is waiting for, and offers the right action (LW-4)', () => {
    const noStyle = create([workItem({ id: 2, statusCode: 'NeedsAttention', parkReasonCode: 'NoStyle', skuCode: null, gtin: null, variantId: null })]);
    expect(textOf(noStyle.fixture)).toContain("The PO's style could not be found.");
    expect(textOf(noStyle.fixture)).toContain('No SKU yet');
    (noStyle.root.querySelector('button') as HTMLButtonElement).click();
    expect(noStyle.resolve).toHaveBeenCalledOnce();
    expect(noStyle.confirmRetired).not.toHaveBeenCalled();

    const retired = create([workItem({ id: 3, statusCode: 'NeedsAttention', parkReasonCode: 'StyleRetired', styleIsActive: false })]);
    expect(textOf(retired.fixture)).toContain('The style is retired');
    expect(textOf(retired.fixture)).toContain('Retired style');
    (retired.root.querySelector('button') as HTMLButtonElement).click();
    expect(retired.confirmRetired).toHaveBeenCalledOnce();
  });

  it('queueing is only offered while tags are still allowed, and previews open the PDF in a new tab', () => {
    const open = create([workItem({ id: 8 })]);
    const queueButton = Array.from(open.root.querySelectorAll('button')).find((b) => b.textContent?.includes('Queue tags')) as HTMLButtonElement;
    expect(queueButton.disabled).toBe(false);
    queueButton.click();
    expect(open.queue).toHaveBeenCalledWith(expect.objectContaining({ id: 8 }));
    const preview = open.root.querySelector('a[target="_blank"]') as HTMLAnchorElement;
    expect(preview.href).toBe('http://api/x/8/tag-preview');
    expect(preview.rel).toContain('noopener');

    const full = create([workItem({ remainingQty: 0 })]);
    const disabled = Array.from(full.root.querySelectorAll('button')).find((b) => b.textContent?.includes('Queue tags')) as HTMLButtonElement;
    expect(disabled.disabled).toBe(true);
  });

  it('is a real table with a caption and column headers (NFR-19)', () => {
    const { root } = create([workItem()]);

    expect(root.querySelector('table caption')?.textContent).toContain('Accepted batches awaiting tags');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
