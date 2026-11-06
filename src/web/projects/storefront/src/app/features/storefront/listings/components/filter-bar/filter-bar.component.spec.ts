import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { ListingOptions } from '../../models/listing.model';
import { EMPTY_FILTER, ListingFilter } from '../../models/listing-query';
import { FilterBarComponent } from './filter-bar.component';

const options: ListingOptions = {
  sizes: [{ value: '5-6y', label: '5-6 Years' }, { value: '7-8y', label: '7-8 Years' }],
  colours: [{ value: 'red', label: 'Red' }, { value: 'navy', label: 'Navy' }],
  priceMin: 500,
  priceMax: 4000,
  ages: [{ value: 'toddler', label: 'Toddler' }],
};

interface Setup {
  filter?: ListingFilter;
  total?: number;
  countFor?: (f: ListingFilter) => Observable<number | null>;
  extra?: Record<string, string>;
  options?: ListingOptions;
}

function render(s: Setup = {}): { f: ComponentFixture<FilterBarComponent>; el: HTMLElement; applied: ReturnType<typeof vi.fn> } {
  vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener: () => undefined, removeEventListener: () => undefined }));
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const f = TestBed.createComponent(FilterBarComponent);
  f.componentRef.setInput('options', s.options ?? options);
  f.componentRef.setInput('filter', s.filter ?? EMPTY_FILTER);
  f.componentRef.setInput('action', '/girls');
  f.componentRef.setInput('total', s.total ?? 48);
  f.componentRef.setInput('extra', s.extra ?? {});
  if (s.countFor) f.componentRef.setInput('countFor', s.countFor);
  f.detectChanges();
  const applied = vi.fn();
  f.componentInstance.applied.subscribe(applied);
  return { f, el: f.nativeElement as HTMLElement, applied };
}
const desktop = (el: HTMLElement) => el.querySelector<HTMLElement>('.rp-filterbar__desktop')!;
const phone = (el: HTMLElement) => el.querySelector<HTMLElement>('.rp-filterbar__phone')!;

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

describe('FilterBarComponent', () => {
  it('FLT-1 the desktop bar has a chip per filter group that has options, the count and a native sort select', () => {
    const { el } = render();
    const chips = [...desktop(el).querySelectorAll('details.rp-chip-pop > summary.rp-chip')].map((s) => s.textContent!.trim());
    expect(chips).toEqual(['Age', 'Size', 'Colour', 'Price']);
    expect(desktop(el).textContent).toContain('48 pieces');
    expect(desktop(el).querySelector('select[name="sort"]')).not.toBeNull();
  });

  it('FLT-1 a group with no options has no chip', () => {
    const { el } = render({ options: { ...options, colours: [], ages: [] } });
    const chips = [...desktop(el).querySelectorAll('summary.rp-chip')].map((s) => s.textContent!.trim());
    expect(chips).toEqual(['Size', 'Price']);
  });

  it('FLT-2 a chip opens a panel with only that group, and a selected group shows as an ink chip with its count and a remove link', () => {
    const { el } = render({ filter: { ...EMPTY_FILTER, sizes: ['5-6y', '7-8y'] } });
    const wraps = [...desktop(el).querySelectorAll('.rp-chipwrap')];
    const size = wraps[1];
    expect(size.querySelector('summary')!.textContent).toContain('Size');
    expect(size.querySelector('summary')!.textContent).toContain('(2)');
    expect(size.querySelector('summary')!.classList.contains('is-on')).toBe(true);
    expect(size.querySelectorAll('fieldset').length).toBe(1);
    const remove = size.querySelector<HTMLAnchorElement>('a.rp-chip__x')!;
    expect(remove.getAttribute('aria-label')).toBe('Remove Size filter');
    expect(remove.getAttribute('href')).toBe('/girls');
    expect(wraps[0].querySelector('a.rp-chip__x')).toBeNull();
  });

  it('FLT-2 the remove link keeps the other filters and the search text', () => {
    const { el } = render({ filter: { ...EMPTY_FILTER, sizes: ['5-6y'], colours: ['red'] }, extra: { q: 'frock' } });
    const remove = desktop(el).querySelector<HTMLAnchorElement>('.rp-chipwrap:nth-child(2) a.rp-chip__x')!;
    expect(remove.getAttribute('href')).toBe('/girls?q=frock&colour=red');
  });

  it('FLT-2 Escape closes an open popover and returns focus to its chip; an outside click closes it too', () => {
    const { f, el } = render();
    const d = desktop(el).querySelector<HTMLDetailsElement>('details.rp-chip-pop')!;
    d.open = true;
    d.querySelector<HTMLElement>('input')!.focus();
    d.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    f.detectChanges();
    expect(d.open).toBe(false);
    expect(document.activeElement).toBe(d.querySelector('summary'));
    d.open = true;
    document.body.click();
    expect(d.open).toBe(false);
  });

  it('FLT-3 the phone bar has one "Filter and sort" disclosure that holds every group and a Show N pieces button', () => {
    const { el } = render();
    const sheet = phone(el).querySelector('details.rp-filterbar__sheet')!;
    expect(sheet.querySelector('summary')!.textContent).toContain('Filter and sort');
    expect([...sheet.querySelectorAll('legend')].map((l) => l.textContent!.trim())).toEqual(['Age', 'Size', 'Colour', 'Price (Rs.)', 'Sort']);
    expect(sheet.querySelector('button[type="submit"]')!.textContent).toContain('Show 48 pieces');
    expect(sheet.querySelectorAll('.rp-swatchbox').length).toBe(2);
  });

  it('FLT-10 the phone sheet has a visible Close button that closes it and returns focus to the summary', () => {
    const { el } = render();
    const sheet = phone(el).querySelector<HTMLDetailsElement>('details.rp-filterbar__sheet')!;
    sheet.open = true;
    sheet.querySelector<HTMLButtonElement>('button[aria-label="Close filters"]')!.click();
    expect(sheet.open).toBe(false);
    expect(document.activeElement).toBe(sheet.querySelector('summary'));
  });

  it('FLT-6 both forms are plain GET forms to the listing address, carrying the search text', () => {
    const { el } = render({ extra: { q: 'frock' } });
    for (const form of el.querySelectorAll('form')) {
      expect(form.getAttribute('method')).toBe('get');
      expect(form.getAttribute('action')).toBe('/girls');
      expect(form.querySelector<HTMLInputElement>('input[type="hidden"][name="q"]')!.value).toBe('frock');
    }
  });

  it('FLT-6 submitting emits the filter read from the form, from page 1, and does not post', () => {
    const { el, applied } = render({ filter: { ...EMPTY_FILTER, page: 3 } });
    const form = desktop(el).querySelector('form')!;
    form.querySelector<HTMLInputElement>('input[name="size"][value="7-8y"]')!.checked = true;
    const event = new Event('submit', { cancelable: true, bubbles: true });
    form.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    expect(applied).toHaveBeenCalledTimes(1);
    expect(applied.mock.calls[0][0]).toMatchObject({ sizes: ['7-8y'], page: 1 });
  });

  it('FLT-13 changing an option in the sheet updates the Show N pieces count from the server after a short pause', () => {
    vi.useFakeTimers();
    const countFor = vi.fn(() => of(7));
    const { f, el } = render({ countFor });
    const form = phone(el).querySelector('form')!;
    form.querySelector<HTMLInputElement>('input[name="colour"][value="red"]')!.checked = true;
    form.dispatchEvent(new Event('change', { bubbles: true }));
    expect(countFor).not.toHaveBeenCalled();
    vi.advanceTimersByTime(300);
    f.detectChanges();
    expect(countFor).toHaveBeenCalledTimes(1);
    expect((countFor.mock.calls[0] as unknown as [ListingFilter])[0].colours).toEqual(['red']);
    expect(phone(el).querySelector('button[type="submit"]')!.textContent).toContain('Show 7 pieces');
  });

  it('FLT-13 several quick changes make one request, and one piece is singular', () => {
    vi.useFakeTimers();
    const countFor = vi.fn(() => of(1));
    const { f, el } = render({ countFor });
    const form = phone(el).querySelector('form')!;
    for (let i = 0; i < 4; i++) form.dispatchEvent(new Event('change', { bubbles: true }));
    vi.advanceTimersByTime(300);
    f.detectChanges();
    expect(countFor).toHaveBeenCalledTimes(1);
    expect(phone(el).querySelector('button[type="submit"]')!.textContent).toContain('Show 1 piece');
    expect(phone(el).querySelector('button[type="submit"]')!.textContent).not.toContain('pieces');
  });

  it('FLT-13 a failed or unknown count keeps the last number and the button still applies the filter', () => {
    vi.useFakeTimers();
    const countFor = vi.fn(() => throwError(() => new Error('down')));
    const { f, el } = render({ countFor: countFor as unknown as Setup['countFor'] });
    phone(el).querySelector('form')!.dispatchEvent(new Event('change', { bubbles: true }));
    vi.advanceTimersByTime(300);
    f.detectChanges();
    expect(phone(el).querySelector('button[type="submit"]')!.textContent).toContain('Show 48 pieces');
  });
});
