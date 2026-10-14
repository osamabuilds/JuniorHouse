import { TestBed } from '@angular/core/testing';
import { product } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { GateFailureDto, ProductDto, ProductTab } from '../../models';
import { PublishPanelComponent } from './publish-panel.component';

describe('PublishPanelComponent (spec PRD-3, PRD-4, PRD-6, CHT-3)', () => {
  function create(item: ProductDto = product(), failures: readonly GateFailureDto[] = []) {
    const fixture = TestBed.createComponent(PublishPanelComponent);
    fixture.componentRef.setInput('product', item);
    fixture.componentRef.setInput('failures', failures);
    const events = { publish: vi.fn(), unpublish: vi.fn(), archive: vi.fn(), tab: vi.fn<(tab: ProductTab) => void>() };
    fixture.componentInstance.publish.subscribe(events.publish);
    fixture.componentInstance.unpublish.subscribe(events.unpublish);
    fixture.componentInstance.archive.subscribe(events.archive);
    fixture.componentInstance.tabSelected.subscribe(events.tab);
    fixture.detectChanges();
    const root = fixture.nativeElement as HTMLElement;
    const button = (text: string) => Array.from(root.querySelectorAll('button')).find((b) => b.textContent?.includes(text)) as HTMLButtonElement;
    return { fixture, root, events, button };
  }

  const failures: GateFailureDto[] = [
    { code: 'NO_IMAGE', message: 'Add at least one image.', tab: 'images' },
    { code: 'CARE_MISSING', message: 'Choose at least one care instruction.', tab: 'composition' },
  ];

  it('lists every missing item, each as a link to the tab that fixes it (PRD-4)', () => {
    const { root, events } = create(product(), failures);

    const links = Array.from(root.querySelectorAll('li a')) as HTMLAnchorElement[];
    expect(links.map((a) => a.textContent?.trim())).toEqual(['Go to Images', 'Go to Composition and care']);
    links[1].click();
    expect(events.tab).toHaveBeenCalledWith('composition');
    expect(root.querySelector('[role="alert"]')).not.toBeNull();
  });

  it('says the product is ready when nothing is missing', () => {
    const { fixture } = create();

    expect(textOf(fixture)).toContain('Everything needed is in place');
  });

  it('shows a missing size chart as advice, never as a blocker (CHT-3)', () => {
    const { fixture } = create(product({ gate: { failures: [], warnings: ['NO_SIZE_CHART'] } }));

    expect(textOf(fixture)).toContain('size chart coming soon');
    expect(textOf(fixture)).toContain('Everything needed is in place');
  });

  it('publishes a draft, and counts the variants that can be bought (PRD-13)', () => {
    const { fixture, button, events } = create();

    button('Publish').click();

    expect(events.publish).toHaveBeenCalled();
    expect(textOf(fixture)).toContain('1 of 2 variants can be bought now');
  });

  it('takes a published product off sale (PRD-3)', () => {
    const { button, events } = create(product({ status: 'Published' }));

    button('Take off sale').click();

    expect(events.unpublish).toHaveBeenCalled();
  });

  it('asks twice before archiving, because it cannot be undone (PRD-6)', () => {
    const { fixture, button, events } = create();

    button('Archive').click();
    fixture.detectChanges();
    expect(events.archive).not.toHaveBeenCalled();
    button('Yes, archive it for good').click();

    expect(events.archive).toHaveBeenCalled();
  });

  it('offers nothing to do on an archived product', () => {
    const { root, fixture } = create(product({ status: 'Archived' }));

    expect(root.querySelectorAll('button').length).toBe(0);
    expect(textOf(fixture)).toContain('archived');
  });
});
