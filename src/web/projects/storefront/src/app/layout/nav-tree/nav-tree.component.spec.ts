import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { launchSettings } from '../../shared/data/launch-settings';
import { NavItem } from '../../shared/models/site-settings.model';
import { NavTreeComponent } from './nav-tree.component';

const boys = launchSettings.navigation.find((n) => n.key === 'boys')!;

function render(items: readonly NavItem[], mode: 'list' | 'accordion' = 'list'): ComponentFixture<NavTreeComponent> {
  TestBed.configureTestingModule({ providers: [provideRouter([{ path: '**', children: [] }])] });
  const f = TestBed.createComponent(NavTreeComponent);
  f.componentRef.setInput('items', items);
  f.componentRef.setInput('mode', mode);
  f.detectChanges();
  return f;
}
const links = (f: ComponentFixture<unknown>): string[] => [...(f.nativeElement as HTMLElement).querySelectorAll('a')].map((a) => `${a.textContent!.trim()}=${a.getAttribute('href')}`);

describe('NavTreeComponent', () => {
  it('SC-3a lists members and their further members as real links, nested in lists', () => {
    const f = render(boys.children);
    const out = links(f);
    expect(out).toContain('Tees and shirts=/boys/tees-and-shirts');
    expect(out).toContain('Polo shirts=/boys/tees-and-shirts/polo-shirts');
    expect(out).toContain('Casual shirts=/boys/casual-shirts');
    expect((f.nativeElement as HTMLElement).querySelectorAll('ul ul').length).toBeGreaterThan(0);
  });

  it('SC-7 accordion mode wraps each member that has members in details/summary, with a link to the member itself', () => {
    const f = render(boys.children, 'accordion');
    const el = f.nativeElement as HTMLElement;
    const summaries = [...el.querySelectorAll('summary')].map((s) => s.textContent!.trim());
    expect(summaries).toEqual(expect.arrayContaining(['Tees and shirts', 'Bottoms']));
    expect(links(f)).toContain('All Tees and shirts=/boys/tees-and-shirts');
    expect(links(f)).toContain('Casual shirts=/boys/casual-shirts');
  });

  it('SC-16 every link is at least 44px tall by class contract (rp-navlink)', () => {
    const f = render(boys.children);
    const a = (f.nativeElement as HTMLElement).querySelector('a')!;
    expect(a.classList.contains('rp-navlink')).toBe(true);
  });

  it('an item with an unsafe target renders as text, never as a link', () => {
    const f = render([{ key: 'x', label: 'Bad', kind: 'link', target: { kind: 'external', url: 'javascript:alert(1)' }, enabled: true, sort: 1, children: [] }]);
    expect(links(f)).toEqual([]);
    expect((f.nativeElement as HTMLElement).textContent).toContain('Bad');
  });

  it('emits navigate when a link is chosen so the host can close its panel', async () => {
    const f = render(boys.children);
    let n = 0;
    f.componentInstance.navigate.subscribe(() => n++);
    (f.nativeElement as HTMLElement).querySelector<HTMLAnchorElement>('a')!.click();
    await f.whenStable();
    expect(n).toBe(1);
  });
});
