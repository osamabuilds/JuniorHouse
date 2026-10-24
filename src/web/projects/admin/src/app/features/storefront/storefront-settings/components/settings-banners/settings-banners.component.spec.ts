import { TestBed } from '@angular/core/testing';
import { ApiError } from '@core/http';
import { buttonNamed, hasButton, textContent } from '@app/testing/dom-helpers';
import { SettingsBannersComponent } from './settings-banners.component';

describe('SettingsBannersComponent (spec ST-4, ST-14)', () => {
  function create(inputs: { notice?: string | null; loadError?: ApiError | null; saveError?: ApiError | null; generalFields?: string[] }) {
    const fixture = TestBed.createComponent(SettingsBannersComponent);
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }

    const events: string[] = [];
    fixture.componentInstance.dismissed.subscribe(() => events.push('dismissed'));
    fixture.componentInstance.reload.subscribe(() => events.push('reload'));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, events };
  }

  it('shows nothing when there is nothing to say', () => {
    expect(textContent(create({}).root).trim()).toBe('');
  });

  it('announces what was saved, politely, with a way to dismiss it', () => {
    const { root, events } = create({ notice: 'Saved. It goes live when you publish.' });

    expect(root.querySelector('[role="status"]')?.textContent).toContain('Saved.');
    buttonNamed(root, 'Dismiss').click();
    expect(events).toEqual(['dismissed']);
  });

  it('shows why a load failed', () => {
    expect(textContent(create({ loadError: { status: 500, message: 'Something went wrong on our side.', fieldErrors: {} } }).root)).toContain('Something went wrong');
  });

  it('a conflict says someone else saved first and offers the reload (ST-14)', () => {
    const { root, events } = create({ saveError: { status: 409, message: 'Someone else changed the settings since you opened them.', fieldErrors: {} } });

    expect(textContent(root)).toContain('Someone else changed the settings');
    buttonNamed(root, /Reload/).click();
    expect(events).toEqual(['reload']);
  });

  it('when the server marked fields, points at them instead of repeating every message', () => {
    const { root } = create({ saveError: { status: 400, message: 'Choose where "Help" links to.', fieldErrors: { 'footer.Help': ['Choose where "Help" links to.'] } } });

    expect(textContent(root)).toContain("This can't be saved yet. Fix what is marked below");
    expect(textContent(root)).not.toContain('Choose where "Help" links to.');
    expect(hasButton(root, /Reload/)).toBe(false);
  });

  it('shows messages about the whole part, such as an empty menu, in the banner', () => {
    const { root } = create({
      generalFields: ['navigation'],
      saveError: { status: 400, message: 'x', fieldErrors: { navigation: ['The menu has no links. Add at least one enabled link.'] } },
    });

    expect(textContent(root)).toContain('The menu has no links');
  });

  it('shows an error with no fields in the server words', () => {
    expect(textContent(create({ saveError: { status: 500, message: 'Something went wrong on our side. Please try again in a moment.', fieldErrors: {} } }).root))
      .toContain('Please try again');
  });
});
