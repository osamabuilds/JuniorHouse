import { DraftSettings, SettingItem, SettingKey } from '../models';

/** The Settings screen's values, one field each, plus the popular-search chips. */
export interface ContactValues {
  readonly brandName: string;
  readonly tagline: string;
  readonly whatsapp: string;
  readonly email: string;
  readonly hours: string;
  readonly announcement: string;
  readonly announcementOn: boolean;
  readonly popularSearches: readonly string[];
}

const valueOf = (items: readonly SettingItem[], key: SettingKey): string => items.find((i) => i.key === key)?.value ?? '';

export function toContactValues(draft: DraftSettings | null): ContactValues {
  const items = draft?.items ?? [];
  return {
    brandName: valueOf(items, 'BrandName'),
    tagline: valueOf(items, 'Tagline'),
    whatsapp: valueOf(items, 'Whatsapp'),
    email: valueOf(items, 'Email'),
    hours: valueOf(items, 'Hours'),
    announcement: valueOf(items, 'Announcement'),
    announcementOn: valueOf(items, 'AnnouncementOn') === 'true',
    popularSearches: draft?.popularSearches ?? [],
  };
}

/** Every setting is sent, empty ones included, so clearing a field clears it on the shop. */
export function toSettingItems(values: ContactValues): SettingItem[] {
  return [
    { key: 'BrandName', value: values.brandName.trim() },
    { key: 'Tagline', value: values.tagline.trim() },
    { key: 'Whatsapp', value: values.whatsapp.replace(/\s+/g, '') },
    { key: 'Email', value: values.email.trim() },
    { key: 'Hours', value: values.hours.trim() },
    { key: 'Announcement', value: values.announcement.trim() },
    { key: 'AnnouncementOn', value: values.announcementOn ? 'true' : 'false' },
  ];
}

export function cleanSearches(searches: readonly string[]): string[] {
  return searches.map((s) => s.trim()).filter((s) => s.length > 0);
}
