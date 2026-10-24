import { draftSettings } from '@app/testing/storefront-fixtures';
import { cleanSearches, toContactValues, toSettingItems } from './contact.util';

describe('contact values (spec ST-4, S-3)', () => {
  it('reads each setting out of the draft, and treats a missing one as empty', () => {
    const values = toContactValues(draftSettings());

    expect(values).toMatchObject({ brandName: 'Romp', whatsapp: '+923315613217', email: '', announcementOn: false });
    expect(values.popularSearches).toEqual(['Dresses', 'Shorts']);
  });

  it('starts empty before the draft has loaded', () => {
    expect(toContactValues(null)).toMatchObject({ brandName: '', popularSearches: [] });
  });

  it('sends every setting, so clearing a field clears it on the shop', () => {
    const items = toSettingItems({ ...toContactValues(draftSettings()), email: '', announcementOn: true, whatsapp: '+92 331 5613217 ' });

    expect(items.map((i) => i.key)).toEqual(['BrandName', 'Tagline', 'Whatsapp', 'Email', 'Hours', 'Announcement', 'AnnouncementOn']);
    expect(items.find((i) => i.key === 'Email')?.value).toBe('');
    expect(items.find((i) => i.key === 'AnnouncementOn')?.value).toBe('true');
    expect(items.find((i) => i.key === 'Whatsapp')?.value).toBe('+923315613217');
  });

  it('drops empty popular searches', () => {
    expect(cleanSearches([' Dresses ', '', '  '])).toEqual(['Dresses']);
  });
});
