import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FactsView } from '../../../site-settings';
import { PageBlock } from '../../../../../shared/models/site-settings.model';
import { PageBlockComponent } from './page-block.component';

const noFacts: FactsView = { whatsapp: null, email: null, hours: null, exchangeWindowDays: null, exchangeConditions: [], deliveryRules: [] };
const facts: FactsView = {
  whatsapp: { href: 'https://wa.me/923315613217', display: '+92 331 5613217' },
  email: 'hello@example.com',
  hours: 'Monday to Saturday, 10 am to 7 pm',
  exchangeWindowDays: 7,
  exchangeConditions: ['Unworn', 'With tags'],
  deliveryRules: [{ city: 'Lahore', minDays: 2, maxDays: 3, charge: 250, freeOver: 8000, courier: 'Courier A', enabled: true }],
};

function render(block: PageBlock, f: FactsView = noFacts): HTMLElement {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({});
  const fx: ComponentFixture<PageBlockComponent> = TestBed.createComponent(PageBlockComponent);
  fx.componentRef.setInput('block', block);
  fx.componentRef.setInput('facts', f);
  fx.detectChanges();
  return fx.nativeElement as HTMLElement;
}

describe('PageBlockComponent', () => {
  it('ST-10 renders heading, paragraph, list and callout as text, never as markup', () => {
    const evil = '<img src=x onerror=alert(1)>';
    expect(render({ type: 'heading', level: 2, text: evil }).querySelector('h2')!.textContent).toBe(evil);
    expect(render({ type: 'heading', level: 3, text: 'Sub' }).querySelector('h3')!.textContent).toBe('Sub');
    expect(render({ type: 'paragraph', text: evil }).querySelector('img')).toBeNull();
    const list = render({ type: 'list', ordered: true, items: ['One', evil] });
    expect(list.querySelectorAll('ol > li').length).toBe(2);
    expect(list.querySelector('img')).toBeNull();
    expect(render({ type: 'callout', text: 'Note' }).querySelector('aside')!.textContent).toContain('Note');
  });

  it('a table block is a real table with column headers', () => {
    const el = render({ type: 'table', caption: 'Sizes', headers: ['Age', 'Chest'], rows: [['2Y', '52 cm']] });
    expect(el.querySelector('caption')!.textContent).toBe('Sizes');
    expect([...el.querySelectorAll('th')].map((h) => h.getAttribute('scope'))).toEqual(['col', 'col']);
    expect(el.querySelectorAll('tbody tr').length).toBe(1);
  });

  it('SC-14 a contact fact shows the configured WhatsApp, email and hours', () => {
    expect(render({ type: 'fact', key: 'contact.whatsapp', label: 'WhatsApp' }, facts).querySelector('a')!.getAttribute('href')).toBe('https://wa.me/923315613217');
    expect(render({ type: 'fact', key: 'contact.email', label: 'Email' }, facts).querySelector('a')!.getAttribute('href')).toBe('mailto:hello@example.com');
    expect(render({ type: 'fact', key: 'contact.hours', label: 'Hours' }, facts).textContent).toContain('Monday to Saturday');
  });

  it('SC-14 exchange and delivery facts come from the settings', () => {
    expect(render({ type: 'fact', key: 'exchange.window', label: 'Window' }, facts).textContent).toContain('7 days');
    expect(render({ type: 'fact', key: 'exchange.conditions', label: 'Conditions' }, facts).querySelectorAll('li').length).toBe(2);
    const table = render({ type: 'fact', key: 'delivery.rules', label: 'Where we deliver' }, facts);
    expect(table.querySelector('table')).not.toBeNull();
    expect(table.textContent).toContain('Lahore');
    expect(table.textContent).toContain('2 to 3 days');
    expect(table.textContent).toContain('Rs. 250');
  });

  it('SC-14 a fact that has not been entered shows nothing, not even its heading', () => {
    for (const key of ['contact.whatsapp', 'contact.email', 'contact.hours', 'exchange.window', 'exchange.conditions', 'delivery.rules']) {
      expect(render({ type: 'fact', key, label: 'Heading' }).textContent!.trim()).toBe('');
    }
  });

  it('an unknown fact key shows nothing', () => {
    expect(render({ type: 'fact', key: 'unknown.key', label: 'X' }, facts).textContent!.trim()).toBe('');
  });

  it('a malformed email fact is not turned into a mailto link', () => {
    const el = render({ type: 'fact', key: 'contact.email', label: 'Email' }, { ...facts, email: 'bad" onclick="x' });
    expect(el.querySelector('a')).toBeNull();
  });
});
