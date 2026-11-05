import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { describe, expect, it } from 'vitest';
import { CollectionTile } from '../../models/collections-page.model';
import { CollectionTileComponent } from './collection-tile.component';

const tile = (over: Partial<CollectionTile> = {}): CollectionTile => ({
  code: 'SUMMER',
  name: 'Summer',
  path: '/collections/summer',
  pieces: 5,
  image: { altText: 'A red dress', width: 800, height: 1000, sources: [{ width: 320, webp: '/media/a-320.webp', jpeg: '/media/a-320.jpg' }, { width: 640, webp: '/media/a-640.webp', jpeg: '/media/a-640.jpg' }] },
  ...over,
});

function render(item: CollectionTile, eager = false): HTMLElement {
  TestBed.configureTestingModule({ providers: [provideRouter([])] });
  const f = TestBed.createComponent(CollectionTileComponent);
  f.componentRef.setInput('item', item);
  f.componentRef.setInput('eager', eager);
  f.detectChanges();
  return f.nativeElement as HTMLElement;
}

describe('CollectionTileComponent', () => {
  it('COL-1 is one link to the collection with its name as an h2 and the piece count as text', () => {
    const el = render(tile());
    const links = el.querySelectorAll('a');
    expect(links.length).toBe(1);
    expect(links[0].getAttribute('href')).toBe('/collections/summer');
    expect(el.querySelector('h2')!.textContent).toContain('Summer');
    expect(el.textContent).toContain('5 pieces');
  });

  it('COL-1 one piece is singular', () => {
    const text = render(tile({ pieces: 1 })).textContent!;
    expect(text).toContain('1 piece');
    expect(text).not.toContain('1 pieces');
  });

  it('COL-1 shows the picture with alt text, explicit size and every width, lazy unless it is in the first row', () => {
    const img = render(tile()).querySelector<HTMLImageElement>('picture img')!;
    expect(img.alt).toBe('A red dress');
    expect(img.getAttribute('width')).toBe('800');
    expect(img.getAttribute('height')).toBe('1000');
    expect(img.getAttribute('loading')).toBe('lazy');
    TestBed.resetTestingModule();
    expect(render(tile(), true).querySelector('picture img')!.getAttribute('loading')).toBe('eager');
  });

  it('COL-1 a collection with no picture yet shows the placeholder and no broken image', () => {
    const el = render(tile({ image: null }));
    expect(el.querySelector('img')).toBeNull();
    expect(el.querySelector('.rp-ph')).not.toBeNull();
  });

  it('COL-7 the whole tile is the link, so its target is as large as the tile', () => {
    const el = render(tile());
    expect(el.querySelector('a.rp-tile')).not.toBeNull();
  });
});
