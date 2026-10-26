import { provideTestStore } from '@app/testing/provide-test-store';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { SessionApiActions } from '@core/auth';
import { Store } from '@ngrx/store';
import { App } from './app';
import { routes } from './app.routes';

const staff = { userId: 7, userName: 'amna.khan', displayName: 'Amna Khan', scope: 'Full', csrfToken: 'c' } as const;

describe('App', () => {
  function signedIn() {
    TestBed.inject(Store).dispatch(SessionApiActions.loadSucceeded({ user: staff }));
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes, withComponentInputBinding()), provideHttpClient(), provideHttpClientTesting(), ...provideTestStore()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the landmark elements exactly once each', () => {
    signedIn();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelectorAll('header').length).toBe(1);
    expect(root.querySelectorAll('nav').length).toBe(1);
    expect(root.querySelectorAll('main').length).toBe(1);
    expect(root.querySelectorAll('footer').length).toBe(1);
  });

  it('renders a nav link for each section', () => {
    signedIn();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    const linkText = Array.from(nav?.querySelectorAll('a') ?? []).map((a) => a.textContent?.trim());

    expect(linkText).toEqual(['Reference Data', 'Styles', 'Catalog', 'Vendors', 'Purchase Orders', 'Production', 'Receiving & QC', 'Labelling', 'Warehouse', 'Storefront', 'Staff']);
  });

  it('hides the admin header, nav and footer on the vendor-facing PO view (SCRUM-93 task 51)', async () => {
    const fixture = TestBed.createComponent(App);
    const router = TestBed.inject(Router);
    fixture.detectChanges();
    await router.navigateByUrl('/purchase-orders/10/vendor-view');
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('header')).toBeNull();
    expect(root.querySelector('nav')).toBeNull();
    expect(root.querySelector('footer')).toBeNull();
    expect(root.querySelectorAll('main').length).toBe(1);
  });

  it('shows no staff header, nav or footer to a visitor who is not signed in (sessions AC-9)', () => {
    TestBed.inject(Store).dispatch(SessionApiActions.loadFailed());
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelector('header')).toBeNull();
    expect(root.querySelector('nav')).toBeNull();
    expect(root.querySelector('footer')).toBeNull();
    expect(root.querySelectorAll('main').length).toBe(1);
  });

  it('names the signed-in person and offers sign-out and sign-out-everywhere (sessions AC-11)', () => {
    signedIn();
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const header = (fixture.nativeElement as HTMLElement).querySelector('header') as HTMLElement;
    const buttons = Array.from(header.querySelectorAll('button')).map((b) => b.textContent?.trim());

    expect(header.textContent).toContain('Amna Khan');
    expect(buttons).toEqual(['Sign out', 'Sign out everywhere']);
  });

  it('offers no sign-out to the development stand-in user, who has no session', () => {
    TestBed.inject(Store).dispatch(SessionApiActions.loadSucceeded({ user: { ...staff, userId: 0, displayName: 'Development user' } }));
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const header = (fixture.nativeElement as HTMLElement).querySelector('header') as HTMLElement;

    expect(header.textContent).toContain('Development user');
    expect(header.querySelectorAll('button')).toHaveLength(0);
  });
});
