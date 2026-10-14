import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AlertBadgeComponent } from './alert-badge.component';

describe('AlertBadgeComponent (spec ALR-8)', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] }));

  /** The badge asks on a zero-delay timer, so the request exists one tick after the component is created. */
  async function create() {
    const fixture = TestBed.createComponent(AlertBadgeComponent);
    fixture.detectChanges();
    await new Promise((resolve) => setTimeout(resolve, 0));
    const http = TestBed.inject(HttpTestingController);
    const answer = async (body: object | string, options?: { status: number; statusText: string }) => {
      http.expectOne((r) => r.url.endsWith('/api/warehouse/alerts/open-count')).flush(body, options);
      await fixture.whenStable();
      fixture.detectChanges();
    };
    return { fixture, root: fixture.nativeElement as HTMLElement, answer };
  }

  it('shows nothing until it hears back, and nothing when no alert is open', async () => {
    const { root, answer } = await create();
    expect(root.textContent?.trim()).toBe('');

    await answer({ open: 0, unacknowledged: 0 });

    expect(root.textContent?.trim()).toBe('');
  });

  it('shows the count in text and in an accessible name, including how many nobody has seen', async () => {
    const { root, answer } = await create();

    await answer({ open: 3, unacknowledged: 2 });

    const badge = root.querySelector('[role="status"]') as HTMLElement;
    expect(badge.textContent?.trim()).toBe('3');
    expect(badge.getAttribute('aria-label')).toBe('3 open stock alerts, 2 not yet seen');
  });

  it('says "alert" in the singular when exactly one is open', async () => {
    const { root, answer } = await create();

    await answer({ open: 1, unacknowledged: 0 });

    expect((root.querySelector('[role="status"]') as HTMLElement).getAttribute('aria-label')).toBe('1 open stock alert');
  });

  it('a failed request quietly shows nothing', async () => {
    const { root, answer } = await create();

    await answer('nope', { status: 500, statusText: 'Server Error' });

    expect(root.textContent?.trim()).toBe('');
  });
});
