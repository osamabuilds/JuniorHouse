import { LinkTarget, MAX_NAV_DEPTH, NavItem, SiteSettings } from '../models/site-settings.model';
import { resolveHref } from './resolve-href';

/** Routes that exist or are owned by later stories. A path target must start with one of these. */
const KNOWN_PATH_ROOTS = ['/girls', '/boys', '/new-in', '/collections', '/sale', '/search'];

function targetProblem(target: LinkTarget | undefined, settings: SiteSettings, where: string): string | null {
  if (!target) return null;
  const href = resolveHref(target);
  if (href === null) return `${where}: dead or unsafe link`;
  if (target.kind === 'page' && !settings.pages.some((p) => p.slug === target.slug)) return `${where}: dead or unsafe link (no page "${target.slug}")`;
  if ((target.kind === 'path' || target.kind === 'category') && !KNOWN_PATH_ROOTS.some((r) => href === r || href.startsWith(`${r}/`))) {
    return `${where}: dead or unsafe link (no route "${href}")`;
  }
  return null;
}

/** Plain list of problems; empty means the settings are safe to render. Used by tests and dev-mode assertions. */
export function validateSettings(settings: SiteSettings): string[] {
  const problems: string[] = [];
  const keys = new Set<string>();

  const walk = (items: readonly NavItem[], depth: number): void => {
    for (const item of items) {
      if (keys.has(item.key)) problems.push(`duplicate navigation key "${item.key}"`);
      keys.add(item.key);
      if (depth > MAX_NAV_DEPTH) problems.push(`"${item.key}" is deeper than ${MAX_NAV_DEPTH} levels`);
      if (item.kind === 'group' && !item.children.some((c) => c.enabled)) problems.push(`group "${item.key}" has no enabled members`);
      const t = targetProblem(item.target, settings, `navigation "${item.key}"`);
      if (t) problems.push(t);
      walk(item.children, depth + 1);
    }
  };
  walk(settings.navigation, 1);

  const ages = new Set<string>();
  for (const age of settings.ages) {
    if (!age.code) problems.push(`age without a code ("${age.label}")`);
    else if (ages.has(age.code)) problems.push(`duplicate age code "${age.code}"`);
    ages.add(age.code);
  }

  for (const col of settings.footer) {
    for (const link of col.links) {
      const t = targetProblem(link.target, settings, `footer "${col.title} / ${link.label}"`);
      if (t) problems.push(t);
    }
  }
  return problems;
}
