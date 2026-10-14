import { TestBed } from '@angular/core/testing';
import { PoFileDto } from '../../models';
import { PoFilesPanelComponent } from './po-files-panel.component';

const file = (overrides: Partial<PoFileDto>): PoFileDto => ({
  id: 1,
  fileName: 'spec.pdf',
  categoryId: 1,
  isVendorVisible: true,
  fileSizeBytes: 100,
  uploadedBy: 'dev',
  uploadedAt: '2026-09-29T10:00:00Z',
  addedInRevisionNumber: null,
  retiredInRevisionNumber: null,
  ...overrides,
});

describe('PoFilesPanelComponent (SCRUM-93 task 52, AC-40..AC-42)', () => {
  function create(statusId: number, files: PoFileDto[]) {
    const fixture = TestBed.createComponent(PoFilesPanelComponent);
    fixture.componentRef.setInput('downloadUrlFor', (fileId: number) => `http://api/api/purchase-orders/10/files/${fileId}`);
    fixture.componentRef.setInput('statusId', statusId);
    fixture.componentRef.setInput('files', files);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  const removeButtons = (root: HTMLElement): HTMLButtonElement[] =>
    Array.from(root.querySelectorAll('button')).filter((b) => b.textContent?.trim() === 'Remove');

  it('groups files into vendor-visible and internal, with download links', () => {
    const root = create(1, [file({}), file({ id: 2, fileName: 'cost.pdf', categoryId: 6, isVendorVisible: false })]);

    const headings = Array.from(root.querySelectorAll('h4')).map((h) => h.textContent?.trim());
    expect(headings).toEqual(['Sent to the vendor', 'Internal only']);
    expect((root.querySelector('a') as HTMLAnchorElement).getAttribute('href')).toBe('http://api/api/purchase-orders/10/files/1');
  });

  it('lets a Draft PO remove any file and offers every category', () => {
    const root = create(1, [file({}), file({ id: 2, categoryId: 6, isVendorVisible: false })]);

    expect(removeButtons(root).every((b) => !b.disabled)).toBe(true);
    expect(root.querySelectorAll('#file-category option').length).toBe(9);
  });

  it('after Send, locks vendor-visible files and points to Amend, and keeps internal files', () => {
    const root = create(2, [file({}), file({ id: 2, fileName: 'cost.pdf', categoryId: 6, isVendorVisible: false })]);

    const [vendorRemove, internalRemove] = removeButtons(root);
    expect(vendorRemove.disabled).toBe(true);
    expect(vendorRemove.getAttribute('title')).toContain('Use Amend');
    expect(internalRemove.disabled).toBe(true);
    expect(internalRemove.getAttribute('title')).toContain('cannot be removed');
    // only internal categories can still be added directly
    expect(root.querySelectorAll('#file-category option').length).toBe(4);
    expect(root.textContent).toContain('through an amendment');
  });

  it('for a Cancelled PO offers no upload and keeps downloads', () => {
    const root = create(4, [file({})]);

    expect(root.querySelector('#file-input')).toBeNull();
    expect(root.querySelector('a')).not.toBeNull();
    expect(removeButtons(root)[0].disabled).toBe(true);
  });
});
