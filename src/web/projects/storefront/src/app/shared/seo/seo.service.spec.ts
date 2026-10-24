import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { Meta, Title } from '@angular/platform-browser';
import { SITE_URL, SeoService } from './seo.service';

describe('SeoService', () => {
  function setup() {
    TestBed.configureTestingModule({ providers: [{ provide: SITE_URL, useValue: 'https://shop.example' }] });
    return { seo: TestBed.inject(SeoService), doc: TestBed.inject(DOCUMENT), meta: TestBed.inject(Meta), title: TestBed.inject(Title) };
  }

  it('SC-13 sets a unique title, description, canonical, Open Graph and Twitter tags', () => {
    const { seo, doc, meta, title } = setup();
    seo.set({ title: 'Delivery', description: 'How delivery works.', path: '/delivery', siteName: 'Romp', crumbs: [{ name: 'Home', path: '/' }, { name: 'Delivery', path: '/delivery' }] });
    expect(title.getTitle()).toBe('Delivery | Romp');
    expect(meta.getTag('name="description"')!.content).toBe('How delivery works.');
    expect(doc.head.querySelector('link[rel="canonical"]')!.getAttribute('href')).toBe('https://shop.example/delivery');
    expect(meta.getTag('property="og:title"')!.content).toBe('Delivery | Romp');
    expect(meta.getTag('property="og:url"')!.content).toBe('https://shop.example/delivery');
    expect(meta.getTag('property="og:site_name"')!.content).toBe('Romp');
    expect(meta.getTag('name="twitter:card"')!.content).toBe('summary');
    expect(meta.getTag('name="robots"')).toBeNull();
  });

  it('SC-13 writes BreadcrumbList JSON-LD that matches the visible trail', () => {
    const { seo, doc } = setup();
    seo.set({ title: 'Delivery', description: 'd', path: '/delivery', siteName: 'Romp', crumbs: [{ name: 'Home', path: '/' }, { name: 'Delivery', path: '/delivery' }] });
    const ld = JSON.parse(doc.head.querySelector('script[type="application/ld+json"]')!.textContent!);
    expect(ld['@type']).toBe('BreadcrumbList');
    expect(ld.itemListElement.map((i: { name: string; position: number }) => [i.position, i.name])).toEqual([[1, 'Home'], [2, 'Delivery']]);
    expect(ld.itemListElement[1].item).toBe('https://shop.example/delivery');
  });

  it('calling set again replaces the tags instead of stacking them', () => {
    const { seo, doc } = setup();
    seo.set({ title: 'A', description: 'a', path: '/a', siteName: 'Romp', crumbs: [{ name: 'A', path: '/a' }] });
    seo.set({ title: 'B', description: 'b', path: '/b', siteName: 'Romp', crumbs: [{ name: 'B', path: '/b' }] });
    expect(doc.head.querySelectorAll('link[rel="canonical"]').length).toBe(1);
    expect(doc.head.querySelectorAll('script[type="application/ld+json"]').length).toBe(1);
  });

  it('SC-13 noindex pages say so', () => {
    const { seo, meta } = setup();
    seo.set({ title: 'Not found', description: 'x', path: '/nope', siteName: 'Romp', crumbs: [], noindex: true });
    expect(meta.getTag('name="robots"')!.content).toBe('noindex, nofollow');
  });

  it('ST-10 a title containing markup stays plain text in the tags and the JSON-LD', () => {
    const { seo, doc, title } = setup();
    seo.set({ title: '</script><script>alert(1)</script>', description: 'x', path: '/x', siteName: 'Romp', crumbs: [{ name: '</script>', path: '/x' }] });
    expect(title.getTitle()).toContain('</script>');
    expect(doc.head.querySelectorAll('script[type="application/ld+json"]').length).toBe(1);
    // The only script element is the JSON-LD one: nothing typed into a title became markup.
    expect(doc.head.querySelectorAll('script').length).toBe(1);
    expect(doc.head.querySelector('script')!.textContent).not.toContain('</script');
  });
});
