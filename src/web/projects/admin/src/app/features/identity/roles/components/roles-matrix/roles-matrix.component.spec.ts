import { TestBed } from '@angular/core/testing';
import { RoleMatrixDto } from '../../models';
import { RolesMatrixComponent } from './roles-matrix.component';

const MATRIX: RoleMatrixDto = {
  permissions: [
    { code: 'catalog.read', name: 'View the catalogue', description: 'Styles and products' },
    { code: 'catalog.write', name: 'Change the catalogue', description: null },
    { code: 'inventory.write', name: 'Adjust stock', description: null },
  ],
  roles: [
    { id: 1, code: 'OWNER', name: 'Owner', description: 'Everything', hasAllPermissions: true, permissionCodes: ['catalog.read', 'catalog.write', 'inventory.write'] },
    { id: 2, code: 'CATALOGUE', name: 'Catalogue', description: null, hasAllPermissions: false, permissionCodes: ['catalog.read', 'catalog.write'] },
    { id: 3, code: 'SUPPORT', name: 'Support', description: null, hasAllPermissions: false, permissionCodes: ['catalog.read'] },
  ],
};

describe('RolesMatrixComponent (roles AC-6, AC-7)', () => {
  function create() {
    const fixture = TestBed.createComponent(RolesMatrixComponent);
    fixture.componentRef.setInput('matrix', MATRIX);
    fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement);
  }

  it('is one table with a caption, a column heading per role and a row heading per permission', () => {
    const root = create();

    expect(root.querySelectorAll('h1')).toHaveLength(1);
    expect(root.querySelectorAll('table')).toHaveLength(1);
    expect(root.querySelector('caption')?.textContent).toContain('Which role can do what');
    expect(Array.from(root.querySelectorAll('thead th[scope="col"]')).map((th) => th.textContent?.replace(/\s+/g, ' ').trim())).toEqual([
      'Permission',
      'Owner holds every permission',
      'Catalogue',
      'Support',
    ]);
    expect(Array.from(root.querySelectorAll('tbody th[scope="row"]')).map((th) => th.querySelector('code')?.textContent)).toEqual([
      'catalog.read',
      'catalog.write',
      'inventory.write',
    ]);
  });

  it('says Yes or No in every cell in words, not only with a colour or a tick', () => {
    const root = create();
    const row = (code: string) =>
      Array.from(Array.from(root.querySelectorAll('tbody tr')).find((tr) => tr.querySelector('code')?.textContent === code)?.querySelectorAll('td') ?? []).map((td) => td.textContent?.trim());

    expect(row('catalog.read')).toEqual(['Yes', 'Yes', 'Yes']);
    expect(row('catalog.write')).toEqual(['Yes', 'Yes', 'No']);
    expect(row('inventory.write')).toEqual(['Yes', 'No', 'No']);
  });

  it('notes that the Owner holds everything, including permissions added later', () => {
    const root = create();

    expect(root.textContent).toContain('holds every permission');
  });

  it('shows what each permission is for, and has no controls: it is read-only', () => {
    const root = create();

    expect(root.textContent).toContain('Styles and products');
    expect(root.querySelectorAll('button, input, select, textarea')).toHaveLength(0);
  });
});
