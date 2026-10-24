import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ButtonDirective } from './button.directive';
import { LinkCtaDirective } from '../link-cta/link-cta.directive';
import { IconButtonDirective } from '../icon-button/icon-button.directive';

@Component({
  imports: [ButtonDirective, LinkCtaDirective, IconButtonDirective],
  template: `
    <button rpBtn id="a">Add to bag</button>
    <button rpBtn="ghost" size="sm" id="b">Show more</button>
    <button rpBtn="light" [block]="true" id="c">Join</button>
    <button rpBtn disabled reasonId="why" id="d">Add to bag</button>
    <a rpLink [light]="true" href="/girls" id="e">Shop girls</a>
    <button rpIconBtn aria-label="Search" id="f"></button>
  `,
})
class HostComponent {}

describe('Button directives', () => {
  function render() {
    const f = TestBed.createComponent(HostComponent);
    f.detectChanges();
    return (id: string) => f.nativeElement.querySelector('#' + id) as HTMLElement;
  }

  it('DSF-6 applies variant and size classes to the native element', () => {
    const el = render();
    expect(el('a').className).toBe('rp-btn');
    expect(el('b').classList.contains('rp-btn--ghost')).toBe(true);
    expect(el('b').classList.contains('rp-btn--sm')).toBe(true);
    expect(el('c').classList.contains('rp-btn--light')).toBe(true);
    expect(el('c').classList.contains('rp-btn--block')).toBe(true);
    expect(el('a').tagName).toBe('BUTTON');
  });

  it('DSF-6 links a disabled button to the text that explains why', () => {
    const el = render();
    expect(el('d').getAttribute('aria-describedby')).toBe('why');
    expect(el('a').hasAttribute('aria-describedby')).toBe(false);
  });

  it('DSF-5 gives links and icon buttons their design classes on native elements', () => {
    const el = render();
    expect(el('e').classList.contains('rp-link--light')).toBe(true);
    expect(el('e').tagName).toBe('A');
    expect(el('f').classList.contains('rp-icon-btn')).toBe(true);
  });
});
