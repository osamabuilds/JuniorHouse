import { TestBed } from '@angular/core/testing';
import { snapshotRow } from '@app/testing/storefront-fixtures';
import { buttonNamed, hasButton, textContent } from '@app/testing/dom-helpers';
import { SnapshotRow } from '../../models';
import { SnapshotsTableComponent } from './snapshots-table.component';

describe('SnapshotsTableComponent (spec ST-12)', () => {
  function create(snapshots: SnapshotRow[], restoringVersion: number | null = null, loading = false) {
    const fixture = TestBed.createComponent(SnapshotsTableComponent);
    fixture.componentRef.setInput('snapshots', snapshots);
    fixture.componentRef.setInput('restoringVersion', restoringVersion);
    fixture.componentRef.setInput('loading', loading);
    const restored: number[] = [];
    fixture.componentInstance.restore.subscribe((v) => restored.push(v));
    fixture.detectChanges();
    return { root: fixture.nativeElement as HTMLElement, restored };
  }

  const rows = [snapshotRow({ versionNo: 3, isCurrent: true }), snapshotRow({ versionNo: 2, isCurrent: false, note: '' })];

  it('marks the version on the shop in words, and offers Restore only for the others', () => {
    const { root } = create(rows);

    expect(textContent(root)).toContain('Version 3 on the shop');
    expect(hasButton(root, 'Restore version 3')).toBe(false);
    expect(hasButton(root, 'Restore version 2')).toBe(true);
  });

  it('asks to restore the chosen version', () => {
    const { root, restored } = create(rows);

    buttonNamed(root, 'Restore version 2').click();

    expect(restored).toEqual([2]);
  });

  it('switches Restore off while one runs', () => {
    const { root } = create(rows, 2);

    expect(buttonNamed(root, 'Restore version 2').disabled).toBe(true);
    expect(textContent(root)).toContain('Restoring…');
  });

  it('shows the note, or a dash, and column headers with scope', () => {
    const { root } = create(rows);

    expect(textContent(root)).toContain('March launch');
    expect(Array.from(root.querySelectorAll('thead th')).every((th) => th.getAttribute('scope') === 'col')).toBe(true);
  });

  it('says when nothing has been published yet, and when it is loading', () => {
    expect(textContent(create([]).root)).toContain('Nothing has been published yet.');
    expect(textContent(create([], null, true).root)).toContain('Loading the versions');
  });
});
