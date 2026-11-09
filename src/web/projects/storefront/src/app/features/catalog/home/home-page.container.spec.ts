import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideStore } from '@ngrx/store';
import { of } from 'rxjs';
import { provideNavigation } from '../../storefront/navigation';
import { NavigationApi } from '../../storefront/navigation/services/navigation-api.service';
import { provideSiteSettings } from '../../storefront/site-settings';
import { Home } from '../../storefront/navigation/models/home.model';
import { HomePageContainer } from './home-page.container';

function render(audiences: Home['audiences']): HTMLElement {
  const home: Home = { newArrivals: [], isFallback: false, audiences };
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideStore({}), provideSiteSettings(), provideNavigation(), { provide: NavigationApi, useValue: { getHome: () => of(home), getMenu: () => of({ audiences: [], collections: [] }) } }],
  });
  const fixture = TestBed.createComponent(HomePageContainer);
  fixture.detectChanges();
  TestBed.flushEffects?.();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('HomePageContainer', () => {
  it('audience split: one audience gets the single-tile layout so the row is not half empty', () => {
    const el = render([{ code: 'GIRLS', name: 'Girls', path: '/girls' }]);
    const list = el.querySelector('ul.rp-split')!;
    expect(list.querySelectorAll('li').length).toBe(1);
    expect(list.classList.contains('rp-split--single')).toBe(true);
  });

  it('audience split: two audiences use the normal two-column layout', () => {
    const el = render([
      { code: 'GIRLS', name: 'Girls', path: '/girls' },
      { code: 'BOYS', name: 'Boys', path: '/boys' },
    ]);
    const list = el.querySelector('ul.rp-split')!;
    expect(list.querySelectorAll('li').length).toBe(2);
    expect(list.classList.contains('rp-split--single')).toBe(false);
  });
});
