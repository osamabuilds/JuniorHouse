import { LinkTarget } from '../models/site-settings.model';

const SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const INTERNAL_PATH = /^\/[a-z0-9\-/]*$/;

/** The only place a URL is built from settings. Returns null for anything unsafe, so callers drop the link. */
export function resolveHref(target: LinkTarget): string | null {
  switch (target.kind) {
    case 'page':
      return SLUG.test(target.slug) ? `/${target.slug}` : null;
    case 'collection':
      return SLUG.test(target.code) ? `/collections/${target.code}` : null;
    case 'path':
    case 'category': {
      const p = target.path;
      return INTERNAL_PATH.test(p) && !p.startsWith('//') && !p.includes('//') ? p : null;
    }
    case 'external':
      return /^https:\/\/[^\s/]+(\/[^\s]*)?$/.test(target.url) ? target.url : null;
  }
}
