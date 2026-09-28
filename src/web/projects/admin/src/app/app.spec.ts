import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { App } from './app';
import { routes } from './app.routes';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter(routes, withComponentInputBinding()), provideHttpClient(), provideHttpClientTesting()],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('renders the landmark elements exactly once each', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;

    expect(root.querySelectorAll('header').length).toBe(1);
    expect(root.querySelectorAll('nav').length).toBe(1);
    expect(root.querySelectorAll('main').length).toBe(1);
    expect(root.querySelectorAll('footer').length).toBe(1);
  });

  it('renders a nav link for each of the four Sprint 1 sections', () => {
    const fixture = TestBed.createComponent(App);
    fixture.detectChanges();
    const nav = (fixture.nativeElement as HTMLElement).querySelector('nav');
    const linkText = Array.from(nav?.querySelectorAll('a') ?? []).map((a) => a.textContent?.trim());

    expect(linkText).toEqual(['Reference Data', 'Styles', 'Vendors', 'Purchase Orders']);
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
});
