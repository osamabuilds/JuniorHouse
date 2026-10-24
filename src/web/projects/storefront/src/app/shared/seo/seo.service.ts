import { DOCUMENT } from '@angular/common';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

/** Public origin used to build canonical URLs (SEO feature will set it from configuration). */
export const SITE_URL = new InjectionToken<string>('SITE_URL', { providedIn: 'root', factory: () => 'http://localhost:4000' });

export interface Crumb { readonly name: string; readonly path: string }

export interface SeoInput {
  readonly title: string;
  readonly description: string;
  /** Path of the page, for example /delivery. The canonical is always the page itself. */
  readonly path: string;
  readonly siteName: string;
  readonly crumbs: readonly Crumb[];
  readonly noindex?: boolean;
}

/** Per-page head tags: title, description, canonical, Open Graph, Twitter and BreadcrumbList (SEO-03, 04, 08). */
@Injectable({ providedIn: 'root' })
export class SeoService {
  private readonly titleService = inject(Title);
  private readonly meta = inject(Meta);
  private readonly doc = inject(DOCUMENT);
  private readonly site = inject(SITE_URL);

  set(input: SeoInput): void {
    const title = `${input.title} | ${input.siteName}`;
    const url = this.absolute(input.path);
    this.titleService.setTitle(title);
    this.tag('name', 'description', input.description);
    this.tag('property', 'og:title', title);
    this.tag('property', 'og:description', input.description);
    this.tag('property', 'og:type', 'website');
    this.tag('property', 'og:url', url);
    this.tag('property', 'og:site_name', input.siteName);
    this.tag('name', 'twitter:card', 'summary');
    this.tag('name', 'twitter:title', title);
    this.tag('name', 'twitter:description', input.description);
    if (input.noindex) this.tag('name', 'robots', 'noindex, nofollow');
    else this.meta.removeTag('name="robots"');
    this.canonical(url);
    this.breadcrumbs(input.crumbs);
  }

  private absolute(path: string): string {
    return `${this.site.replace(/\/$/, '')}${path === '/' ? '' : path}` || this.site;
  }

  private tag(attr: 'name' | 'property', key: string, content: string): void {
    this.meta.updateTag({ [attr]: key, content }, `${attr}="${key}"`);
  }

  private canonical(url: string): void {
    let link = this.doc.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.doc.createElement('link');
      link.setAttribute('rel', 'canonical');
      this.doc.head.appendChild(link);
    }
    link.setAttribute('href', url);
  }

  private breadcrumbs(crumbs: readonly Crumb[]): void {
    this.doc.head.querySelectorAll('script[type="application/ld+json"][data-rp="breadcrumbs"]').forEach((n) => n.remove());
    if (crumbs.length === 0) return;
    const script = this.doc.createElement('script');
    script.setAttribute('type', 'application/ld+json');
    script.setAttribute('data-rp', 'breadcrumbs');
    // JSON.stringify never emits a raw "</script>" once "<" is escaped, so the text cannot close the element.
    script.textContent = JSON.stringify({
      '@context': 'https://schema.org',
      '@type': 'BreadcrumbList',
      itemListElement: crumbs.map((c, i) => ({ '@type': 'ListItem', position: i + 1, name: c.name, item: this.absolute(c.path) })),
    }).replace(/</g, '\\u003c');
    this.doc.head.appendChild(script);
  }
}
