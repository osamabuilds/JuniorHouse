import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { variantRow } from '@app/testing/labelling-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { VariantRowDto } from '../../models';
import { VariantTableComponent } from './variant-table.component';

describe('VariantTableComponent (spec VAR-7, VAR-8, VAR-13, PJ-5, PJ-6)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideRouter([])] }));

  function create(variants: VariantRowDto[], loading = false) {
    const fixture = TestBed.createComponent(VariantTableComponent);
    fixture.componentRef.setInput('variants', variants);
    fixture.componentRef.setInput('loading', loading);
    const details = vi.fn();
    const voidRequested = vi.fn();
    fixture.componentInstance.details.subscribe(details);
    fixture.componentInstance.voidRequested.subscribe(voidRequested);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, details, voidRequested };
  }

  it('says when there are no SKUs, and shows loading', () => {
    expect(textOf(create([]).fixture)).toContain('No SKUs found.');
    expect(textOf(create([], true).fixture)).toContain('Loading…');
  });

  it('shows the readable SKU next to the barcode number, with the style, colour and size', () => {
    const text = textOf(create([variantRow()]).fixture);

    expect(text).toContain('ROMP-STY-DRESS-RED-2-3Y');
    expect(text).toContain('0200000000019');
    expect(text).toContain('Floral Summer Dress');
    expect(text).toContain('Red / 2-3 Years');
  });

  it('counts the retired numbers on record, in words', () => {
    expect(textOf(create([variantRow({ retiredCount: 1 })]).fixture)).toContain('1 retired number on record');
    expect(textOf(create([variantRow({ retiredCount: 3 })]).fixture)).toContain('3 retired numbers on record');
  });

  it('reprint and print history are links to the Print queue with the variant, and void emits the row', () => {
    const { root, voidRequested, details } = create([variantRow({ id: 31 })]);

    const links = Array.from(root.querySelectorAll('a')) as HTMLAnchorElement[];
    const reprint = links.find((a) => a.textContent?.includes('Reprint'))!;
    const history = links.find((a) => a.textContent?.includes('Print history'))!;
    expect(reprint.getAttribute('href')).toContain('/labelling/queue');
    expect(reprint.getAttribute('href')).toContain('variantId=31');
    expect(reprint.getAttribute('href')).toContain('reprint=1');
    expect(history.getAttribute('href')).toContain('variantId=31');
    expect(history.getAttribute('href')).not.toContain('reprint=1');

    const buttons = Array.from(root.querySelectorAll('button')) as HTMLButtonElement[];
    buttons.find((b) => b.textContent?.includes('Barcode history'))!.click();
    buttons.find((b) => b.textContent?.includes('Void barcode'))!.click();
    expect(details).toHaveBeenCalledWith(expect.objectContaining({ id: 31 }));
    expect(voidRequested).toHaveBeenCalledWith(expect.objectContaining({ id: 31 }));
  });

  it('a SKU whose barcode was voided offers no reprint or void, and says so (PJ-8)', () => {
    const { fixture, root } = create([variantRow({ activeGtin: null, retiredCount: 1 })]);

    expect(textOf(fixture)).toContain('Voided: none in use');
    const labels = [...Array.from(root.querySelectorAll('a')), ...Array.from(root.querySelectorAll('button'))].map((e) => e.textContent?.trim());
    expect(labels).not.toContain('Reprint');
    expect(labels).not.toContain('Void barcode');
    expect(labels).toContain('Print history');
  });

  it('is a real table with a caption and column headers (NFR-19)', () => {
    const { root } = create([variantRow()]);

    expect(root.querySelector('table caption')?.textContent).toContain('SKU register');
    expect(Array.from(root.querySelectorAll('th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });
});
