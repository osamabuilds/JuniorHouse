import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { launchSettings } from '../../shared/data/launch-settings';
import { MegaMenuComponent } from './mega-menu.component';

const boys = launchSettings.navigation.find((n) => n.key === 'boys')!;

function render(entry = boys, ages = launchSettings.ages) {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', children: [] }])] });
  const f = TestBed.createComponent(MegaMenuComponent);
  f.componentRef.setInput('entry', entry);
  f.componentRef.setInput('ages', ages);
  f.detectChanges();
  return f;
}

describe('MegaMenuComponent', () => {
  it('SC-3a shows the members and further members of the entry as links', () => {
    const el = render().nativeElement as HTMLElement;
    const hrefs = [...el.querySelectorAll('a')].map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/boys/tees-and-shirts');
    expect(hrefs).toContain('/boys/bottoms/shorts');
    expect(hrefs).toContain('/boys/casual-shirts');
  });

  it('SC-5 shows one age link per configured age, carrying the age code as the query parameter', () => {
    const el = render().nativeElement as HTMLElement;
    const ageLinks = [...el.querySelectorAll('.rp-mega__ages a')];
    expect(ageLinks.map((a) => a.textContent!.trim())).toEqual(['1Y', '2Y', '3Y', '4Y', '5Y', '6Y', '7Y', '8Y']);
    expect(ageLinks[3].getAttribute('href')).toBe('/boys?age=4y');
  });

  it('SC-5 leaves the age column out when the entry does not ask for it', () => {
    const el = render({ ...boys, showAges: false }).nativeElement as HTMLElement;
    expect(el.querySelector('.rp-mega__ages')).toBeNull();
  });

  it('SC-3 follows the settings: a renamed member and a new member appear with no code change', () => {
    const edited = { ...boys, children: [{ ...boys.children[0], label: 'Shirts and tees' }, ...boys.children.slice(1), { key: 'extra', label: 'Outerwear', kind: 'link' as const, target: { kind: 'path' as const, path: '/boys/outerwear' }, enabled: true, sort: 9, children: [] }] };
    const text = (render(edited).nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Shirts and tees');
    expect(text).toContain('Outerwear');
  });

  it('is a labelled region', () => {
    const region = (render().nativeElement as HTMLElement).querySelector('[role="region"]')!;
    expect(region.getAttribute('aria-label')).toBe('Boys menu');
  });
});

describe('MegaMenuComponent tile', () => {
  const tile = {
    eyebrow: 'New in',
    line: 'Just arrived',
    path: '/boys/new-in',
    image: { altText: 'A navy shirt, front view', width: 800, height: 1000, sources: [{ width: 320, webp: '/media/a-320.webp', jpeg: '/media/a-320.jpg' }, { width: 640, webp: '/media/a-640.webp', jpeg: '/media/a-640.jpg' }] },
  };

  it('MGT-1 MGT-2 an entry with a tile shows one link to the New in page, named by where it goes, with the eyebrow and the line', () => {
    const el = render({ ...boys, tile }).nativeElement as HTMLElement;
    const links = el.querySelectorAll<HTMLAnchorElement>('a.rp-mega__tile');
    expect(links.length).toBe(1);
    expect(links[0].getAttribute('href')).toBe('/boys/new-in');
    expect(links[0].getAttribute('aria-label')).toBe('Boys, new in');
    expect(links[0].textContent).toContain('New in');
    expect(links[0].textContent).toContain('Just arrived');
  });

  it('MGT-4 the tile picture is lazy, has alt text and explicit size, and offers every width', () => {
    const el = render({ ...boys, tile }).nativeElement as HTMLElement;
    const img = el.querySelector<HTMLImageElement>('.rp-mega__tile img')!;
    expect(img.getAttribute('loading')).toBe('lazy');
    expect(img.alt).toBe('A navy shirt, front view');
    expect(img.getAttribute('width')).toBe('800');
    expect(img.getAttribute('height')).toBe('1000');
    const source = el.querySelector('.rp-mega__tile source[type="image/webp"]')!;
    expect(source.getAttribute('srcset')).toBe('/media/a-320.webp 320w, /media/a-640.webp 640w');
  });

  it('MGT-3 an entry with no tile draws none and the columns are as before', () => {
    const el = render().nativeElement as HTMLElement;
    expect(el.querySelector('.rp-mega__tile')).toBeNull();
    expect(el.querySelector('.rp-mega__tilecol')).toBeNull();
  });

  it('MGT-6 choosing the tile tells the host to close the panel', () => {
    const f = render({ ...boys, tile });
    const navigate = vi.fn();
    f.componentInstance.navigate.subscribe(navigate);
    (f.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a.rp-mega__tile')!.click();
    expect(navigate).toHaveBeenCalledTimes(1);
  });
});
