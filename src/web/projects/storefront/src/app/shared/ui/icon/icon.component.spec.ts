import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { IconComponent, RpIconName } from './icon.component';

const NAMES: RpIconName[] = [
  'search', 'heart', 'bag', 'menu', 'close', 'arrow', 'arrow-left',
  'check', 'alert', 'truck', 'swap', 'chat', 'cash', 'pin',
];

@Component({
  imports: [IconComponent],
  template: `@for (n of names; track n) { <rp-icon [name]="n" /> } <rp-icon name="bag" label="Bag" />`,
})
class HostComponent {
  names = NAMES;
}

describe('IconComponent', () => {
  it('DSF-13 renders every icon with content and hides it from assistive tech by default', () => {
    const f = TestBed.createComponent(HostComponent);
    f.detectChanges();
    const svgs = Array.from(f.nativeElement.querySelectorAll('svg')) as SVGElement[];
    expect(svgs.length).toBe(NAMES.length + 1);
    for (const svg of svgs.slice(0, NAMES.length)) {
      expect(svg.children.length).toBeGreaterThan(0);
      expect(svg.getAttribute('aria-hidden')).toBe('true');
    }
  });

  it('DSF-13 exposes a labelled icon as an image', () => {
    const f = TestBed.createComponent(HostComponent);
    f.detectChanges();
    const labelled = f.nativeElement.querySelector('svg[role="img"]') as SVGElement;
    expect(labelled.getAttribute('aria-label')).toBe('Bag');
    expect(labelled.hasAttribute('aria-hidden')).toBe(false);
  });
});
