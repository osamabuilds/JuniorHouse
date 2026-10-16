import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    }).compileComponents();
  });

  it('DSF-14 renders the shell with the root class and without the scaffold placeholder', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.classList.contains('rp-root')).toBe(true);
    expect(el.textContent).not.toContain('Hello, storefront');
    expect(el.querySelector('router-outlet')).not.toBeNull();
  });
});
