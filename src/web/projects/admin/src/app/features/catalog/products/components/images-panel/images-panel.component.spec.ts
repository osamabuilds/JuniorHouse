import { TestBed } from '@angular/core/testing';
import { image } from '@app/testing/catalog-fixtures';
import { textOf } from '@app/testing/production-fixtures';
import { ImagesPanelComponent } from './images-panel.component';

describe('ImagesPanelComponent (spec MED-1..10, SEO-07)', () => {
  function create(images = [image(), image({ id: 12, fileName: 'IMG_1234.jpg', isMain: false, sortSeq: 2, altText: null, altTextProblem: 'Describe what the image shows.' })], requireAlt = false) {
    const fixture = TestBed.createComponent(ImagesPanelComponent);
    fixture.componentRef.setInput('images', images);
    fixture.componentRef.setInput('colours', [{ id: 1, code: 'RED', name: 'Red', description: null, sortSeq: 1, isActive: true, parentCategoryId: null, defaultAdvancePercent: null }]);
    fixture.componentRef.setInput('fileUrl', (id: number) => `/api/catalog/images/${id}/file`);
    fixture.componentRef.setInput('requireAlt', requireAlt);
    const reordered = vi.fn<(ids: readonly number[]) => void>();
    const deleted = vi.fn<(id: number) => void>();
    const altSaved = vi.fn<(change: { imageId: number; text: string | null }) => void>();
    fixture.componentInstance.reordered.subscribe(reordered);
    fixture.componentInstance.deleted.subscribe(deleted);
    fixture.componentInstance.altSaved.subscribe(altSaved);
    fixture.detectChanges();
    return { fixture, root: fixture.nativeElement as HTMLElement, reordered, deleted, altSaved };
  }

  const button = (root: HTMLElement, text: string, index = 0) =>
    Array.from(root.querySelectorAll('button')).filter((b) => b.textContent?.includes(text))[index] as HTMLButtonElement;

  it('renders every image as an img with alt text, width and height, in a figure with a caption (SEO-07, MED-10)', () => {
    const { root } = create();

    const img = root.querySelector('img') as HTMLImageElement;
    expect(img.getAttribute('alt')).toBe('A red floral dress, front view');
    expect(img.getAttribute('width')).toBe('640');
    expect(img.getAttribute('height')).toBe('480');
    expect(root.querySelectorAll('figure figcaption').length).toBe(2);
  });

  it('marks the first image as the main one (MED-8)', () => {
    const { fixture } = create();

    expect(textOf(fixture)).toContain('Main image');
  });

  it('labels the file picker and every alt text field (NFR-19)', () => {
    const { root } = create();

    expect(root.querySelector('label[for="img-file"]')).not.toBeNull();
    expect(root.querySelector('label[for="alt-11"]')).not.toBeNull();
    expect(root.querySelector('label[for="alt-12"]')?.textContent).toContain('IMG_1234.jpg');
  });

  it('shows why an image would block a publish, and offers the suggestion (MED-1, MED-2)', () => {
    const { fixture, root } = create();

    expect(textOf(fixture)).toContain('Describe what the image shows.');
    button(root, 'Use:').click();
    fixture.detectChanges();
    expect((root.querySelector('#alt-12') as HTMLInputElement).value).toBe('Floral Summer Dress, view 1');
  });

  it('saves an edited alt text', () => {
    const { fixture, root, altSaved } = create();
    const input = root.querySelector('#alt-12') as HTMLInputElement;
    input.value = 'The back of the dress';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    button(root, 'Save alt text', 1).click();

    expect(altSaved).toHaveBeenCalledWith({ imageId: 12, text: 'The back of the dress' });
  });

  it('moves images with buttons, so reordering works by keyboard (MED-8)', () => {
    const { root, reordered } = create();

    button(root, 'Move down', 0).click();

    expect(reordered).toHaveBeenCalledWith([12, 11]);
    expect(button(root, 'Move up', 0).disabled).toBe(true);
    expect(button(root, 'Move down', 1).disabled).toBe(true);
  });

  it('asks twice before deleting an image', () => {
    const { fixture, root, deleted } = create();

    button(root, 'Delete', 0).click();
    fixture.detectChanges();
    expect(deleted).not.toHaveBeenCalled();
    button(root, 'Yes, delete it').click();

    expect(deleted).toHaveBeenCalledWith(11);
  });

  it('asks for a picture first, and for alt text when the product is published (MED-5)', () => {
    const { fixture, root } = create([image()], true);
    const form = root.querySelector('form') as HTMLFormElement;

    form.dispatchEvent(new Event('submit'));
    fixture.detectChanges();

    expect(textOf(fixture)).toContain('Choose a picture first.');
  });
});
