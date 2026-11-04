import { provideTestStore } from '@app/testing/provide-test-store';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { PermissionAccess, SessionApiActions, StorePermissionAccess } from '@core/auth';
import { Store } from '@ngrx/store';
import { App } from './app';
import { routes } from './app.routes';

const ALL = ['ref.read', 'ref.write', 'catalog.read', 'catalog.write', 'procurement.read', 'procurement.write', 'production.read', 'production.write', 'qc.read', 'qc.write', 'labelling.read', 'labelling.write', 'warehouse.read', 'warehouse.write', 'inventory.read', 'inventory.write', 'ops.admin', 'staff.manage', 'audit.read'];
const staff = { userId: 7, userName: 'amna.khan', displayName: 'Amna Khan', scope: 'Full', csrfToken: 'c', permissions: ALL } as const;

describe('App', () => {
  function signedIn() {
    TestBed.inject(Store).dispatch(SessionApiActions.loadSucceeded({ user: staff }));
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes, withComponentInputBinding()), provideHttpClient(), provideHttpClientTesting(), ...provideTestStore(), { provide: PermissionAccess, useFactory: () => new StorePermissionAccess(inject(Store)) }],
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

    expect(linkText).toEqual(['Reference Data', 'Styles', 'Catalog', 'Vendors', 'Purchase Orders', 'Production', 'Receiving & QC', 'Labelling', 'Warehouse', 'Storefront', 'Staff', 'Roles', 'Audit']);
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

  describe('the menu shows only what the roles of the person cover (roles AC-7; the server still checks every call)', () => {
    function menuFor(permissions: string[]): string[] {
      TestBed.inject(Store).dispatch(SessionApiActions.loadSucceeded({ user: { ...staff, permissions } }));
      const fixture = TestBed.createComponent(App);
      fixture.detectChanges();
      const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
      return Array.from(nav?.querySelectorAll('a') ?? []).map((a) => a.textContent?.trim() ?? '');
    }

    it('Catalogue: reference data, styles, catalogue, warehouse, storefront', () => {
      expect(menuFor(['ref.read', 'catalog.read', 'catalog.write', 'warehouse.read'])).toEqual(['Reference Data', 'Styles', 'Catalog', 'Warehouse', 'Storefront']);
    });

    it('Order-ops: reference data, styles, catalogue, warehouse and stock', () => {
      expect(menuFor(['ref.read', 'catalog.read', 'warehouse.read', 'inventory.read'])).toEqual(['Reference Data', 'Styles', 'Catalog', 'Warehouse', 'Storefront']);
    });

    it('Support and Marketing: reference data and the catalogue only', () => {
      expect(menuFor(['ref.read', 'catalog.read'])).toEqual(['Reference Data', 'Styles', 'Catalog', 'Storefront']);
    });

    it('a person who holds nothing sees no section at all', () => {
      expect(menuFor([])).toEqual([]);
    });

    it('only audit.read shows Audit', () => {
      expect(menuFor(['audit.read'])).toEqual(['Audit']);
    });

    it('only staff.manage shows Staff and Roles', () => {
      expect(menuFor(['staff.manage'])).toEqual(['Staff', 'Roles']);
    });
  });
});
