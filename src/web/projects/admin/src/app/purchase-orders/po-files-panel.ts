import { Component, computed, inject, input, output, signal } from '@angular/core';
import { ApiError } from '../core/api-error';
import { selectNumberOrNull } from '../shared/dom-events';
import { FILE_CATEGORY_LABELS, PoApiService, PoFileDto, isVendorVisibleCategory } from './po-api.service';

const DRAFT = 1;
const CANCELLED = 4;

/**
 * SCRUM-93 task 52 (AC-37..AC-46): the PO's files, split into what the vendor sees and what stays
 * internal. Draft: add and remove anything. After Send: internal files can still be added but not
 * removed; vendor-visible files are locked and change only through an amendment - those buttons are
 * disabled with the reason spelled out. Cancelled: read-only, downloads still work.
 */
@Component({
  selector: 'app-po-files-panel',
  template: `
    <section class="files" aria-labelledby="files-heading">
      <h3 id="files-heading">Files</h3>

      @if (error()) {
        <p class="banner banner--error" role="alert">{{ error()?.message }}</p>
      }

      <h4>Sent to the vendor</h4>
      @if (vendorVisible().length === 0) {
        <p class="state-message state-message--inline">No vendor-visible files. A tech pack spec is expected before sending.</p>
      } @else {
        <ul class="files__list">
          @for (file of vendorVisible(); track file.id) {
            <li>
              <a [href]="downloadUrl(file)">{{ file.fileName }}</a>
              <span class="files__meta">
                {{ categoryLabel(file.categoryId) }} &middot; {{ file.uploadedBy }}
                @if (file.addedInRevisionNumber !== null) { &middot; added in Rev {{ file.addedInRevisionNumber }} }
                @if (file.retiredInRevisionNumber !== null) { &middot; retired in Rev {{ file.retiredInRevisionNumber }} }
              </span>
              <button
                type="button"
                class="button button--small"
                [disabled]="!canRemove(file)"
                [attr.title]="removeBlockedReason(file)"
                (click)="remove(file)"
              >
                Remove
              </button>
              @if (removeBlockedReason(file); as reason) {
                <span class="files__reason">{{ reason }}</span>
              }
            </li>
          }
        </ul>
      }

      <h4>Internal only</h4>
      @if (internal().length === 0) {
        <p class="state-message state-message--inline">No internal files.</p>
      } @else {
        <ul class="files__list">
          @for (file of internal(); track file.id) {
            <li>
              <a [href]="downloadUrl(file)">{{ file.fileName }}</a>
              <span class="files__meta">{{ categoryLabel(file.categoryId) }} &middot; {{ file.uploadedBy }}</span>
              <button
                type="button"
                class="button button--small"
                [disabled]="!canRemove(file)"
                [attr.title]="removeBlockedReason(file)"
                (click)="remove(file)"
              >
                Remove
              </button>
              @if (removeBlockedReason(file); as reason) {
                <span class="files__reason">{{ reason }}</span>
              }
            </li>
          }
        </ul>
      }

      @if (statusId() !== 4) {
        <div class="files__upload">
          <div class="form-field">
            <label for="file-category">Category</label>
            <select id="file-category" [value]="categoryId()" (change)="onCategory($event)">
              @for (category of allowedCategories(); track category.id) {
                <option [value]="category.id">{{ category.name }}</option>
              }
            </select>
          </div>
          <div class="form-field">
            <label for="file-input">File (PDF, PNG, JPEG, Excel or Word)</label>
            <input id="file-input" type="file" [disabled]="uploading()" (change)="onFile($event)" />
          </div>
          @if (statusId() !== 1) {
            <p class="field-hint">
              This PO has been sent, so vendor-visible files can only be added through an amendment. Internal
              files can still be added here.
            </p>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .files__list {
      list-style: none;
      margin: 0 0 var(--space-4);
      padding: 0;
      display: grid;
      gap: var(--space-2);
    }
    .files__list li {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: var(--space-2);
    }
    .files__meta,
    .files__reason {
      color: var(--color-ink-soft);
      font-size: 0.8125rem;
    }
    .files__upload {
      display: grid;
      gap: var(--space-3);
      max-width: 420px;
    }
  `,
})
export class PoFilesPanel {
  private readonly poApi = inject(PoApiService);

  readonly poId = input.required<number>();
  readonly statusId = input.required<number>();
  readonly files = input.required<readonly PoFileDto[]>();

  /** Emitted after any successful upload/remove so the parent reloads the list. */
  readonly changed = output<void>();

  readonly error = signal<ApiError | null>(null);
  readonly uploading = signal(false);
  readonly categoryId = signal(6);

  readonly vendorVisible = computed(() => this.files().filter((file) => file.isVendorVisible));
  readonly internal = computed(() => this.files().filter((file) => !file.isVendorVisible));

  /** After Send, only internal categories can be added directly (AC-40, AC-41). */
  readonly allowedCategories = computed(() =>
    Object.entries(FILE_CATEGORY_LABELS)
      .map(([id, name]) => ({ id: +id, name }))
      .filter((category) => this.statusId() === DRAFT || !isVendorVisibleCategory(category.id)),
  );

  categoryLabel(categoryId: number): string {
    return FILE_CATEGORY_LABELS[categoryId] ?? `#${categoryId}`;
  }

  downloadUrl(file: PoFileDto): string {
    return this.poApi.fileDownloadUrl(this.poId(), file.id);
  }

  canRemove(file: PoFileDto): boolean {
    return this.statusId() === DRAFT && file.retiredInRevisionNumber === null;
  }

  removeBlockedReason(file: PoFileDto): string | null {
    if (this.canRemove(file)) {
      return null;
    }
    if (this.statusId() === CANCELLED) {
      return 'This PO is cancelled; its files are kept as they are.';
    }
    return file.isVendorVisible
      ? 'This PO has been sent. Use Amend to retire a vendor-visible file.'
      : 'This PO has been sent. Internal files stay on record and cannot be removed.';
  }

  onCategory(event: Event): void {
    this.categoryId.set(selectNumberOrNull(event) ?? 6);
  }

  onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.error.set(null);
    this.poApi.uploadFile(this.poId(), this.categoryId(), file).subscribe({
      next: () => {
        this.uploading.set(false);
        input.value = '';
        this.changed.emit();
      },
      error: (error: ApiError) => {
        this.uploading.set(false);
        this.error.set(error);
      },
    });
  }

  remove(file: PoFileDto): void {
    this.error.set(null);
    this.poApi.removeFile(this.poId(), file.id).subscribe({
      next: () => this.changed.emit(),
      error: (error: ApiError) => this.error.set(error),
    });
  }
}
