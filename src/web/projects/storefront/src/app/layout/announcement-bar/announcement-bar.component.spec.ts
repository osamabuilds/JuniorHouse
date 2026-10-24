import { TestBed } from '@angular/core/testing';
import { Store, provideStore } from '@ngrx/store';
import { SiteSettingsActions, provideSiteSettings } from '../../features/storefront/site-settings';
import { launchSettings } from '../../shared/data/launch-settings';
import { AnnouncementBarComponent } from './announcement-bar.component';

function render(announcement: { text: string; enabled: boolean }) {
  TestBed.configureTestingModule({ providers: [provideStore({}), provideSiteSettings()] });
  TestBed.inject(Store).dispatch(SiteSettingsActions.loadSuccess({ settings: { ...launchSettings, announcement }, source: 'api' }));
  const f = TestBed.createComponent(AnnouncementBarComponent);
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('AnnouncementBarComponent', () => {
  it('Q-SC-8 shows the configured text when switched on', () => {
    expect(render({ text: 'Free delivery this week', enabled: true }).textContent).toContain('Free delivery this week');
  });
  it('Q-SC-8 renders nothing when switched off or empty', () => {
    expect(render({ text: 'Hidden', enabled: false }).querySelector('.rp-ann')).toBeNull();
    TestBed.resetTestingModule();
    expect(render({ text: '   ', enabled: true }).querySelector('.rp-ann')).toBeNull();
  });
  it('ST-10 shows markup as text', () => {
    const el = render({ text: '<img src=x onerror=alert(1)>', enabled: true });
    expect(el.querySelector('img')).toBeNull();
    expect(el.textContent).toContain('<img');
  });
});
