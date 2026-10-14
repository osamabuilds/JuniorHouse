import { ChangeDetectionStrategy, Component, computed, effect, input, output, signal, untracked } from '@angular/core';
import { LookupDto } from '@features/reference/lookups';
import { inputValue, selectNumberOrNull } from '@shared';
import { ProductImageDto, UploadImageValue } from '../../models';

/**
 * The Images tab (MED-1..10, SEO-07): upload JPEG, PNG or WebP pictures for the product or one colour, describe each one in words
 * (alt text, required to publish), put them in order (the first is the main image) and delete them. Every control is a real
 * label, input or button, so it works by keyboard. Images keep their width and height so the page can reserve space.
 */
@Component({
  selector: 'app-images-panel',
  template: `
    <form class="card" (submit)="$event.preventDefault(); upload()" aria-labelledby="img-add-heading">
      <h3 id="img-add-heading">Add an image</h3>
      <div class="form-grid">
        <div class="form-field">
          <label for="img-file">Picture (JPEG, PNG or WebP)</label>
          <input id="img-file" type="file" accept="image/jpeg,image/png,image/webp" [disabled]="disabled()" (change)="onFile($event)" />
        </div>
        <div class="form-field">
          <label for="img-colour">Shows colour</label>
          <select id="img-colour" [disabled]="disabled()" (change)="onColour($event)">
            <option value="">Whole product</option>
            @for (colour of colours(); track colour.id) {
              <option [value]="colour.id">{{ colour.name }}</option>
            }
          </select>
        </div>
        <div class="form-field">
          <label for="img-alt">Alt text{{ requireAlt() ? '' : ' (can be added later)' }}</label>
          <input id="img-alt" type="text" maxlength="125" [disabled]="disabled()" [value]="newAlt()" (input)="newAlt.set(inputText($event))" />
        </div>
      </div>
      @if (formProblem()) {
        <p class="field-error" role="alert">{{ formProblem() }}</p>
      }
      <div class="form-actions">
        <button type="submit" class="button button--primary" [disabled]="disabled() || saving()">{{ saving() ? 'Uploading…' : 'Add image' }}</button>
      </div>
    </form>

    @if (images().length === 0) {
      <p class="state-message">No images yet. A product needs at least one to be published.</p>
    } @else {
      <ol class="image-list">
        @for (image of images(); track image.id; let index = $index; let last = $last) {
          <li class="card image-list__item">
            <figure>
              <img [src]="fileUrl()(image.id)" [alt]="image.altText ?? ''" [attr.width]="image.width" [attr.height]="image.height" loading="lazy" />
              <figcaption>
                {{ image.fileName }} · {{ image.width }}×{{ image.height }}
                @if (image.isMain) {
                  <span class="badge">Main image</span>
                }
              </figcaption>
            </figure>
            <div class="form-field">
              <label [for]="'alt-' + image.id">Alt text for {{ image.fileName }}</label>
              <input
                [id]="'alt-' + image.id"
                type="text"
                maxlength="125"
                [disabled]="disabled()"
                [value]="altOf(image)"
                [attr.aria-invalid]="image.altTextProblem ? 'true' : null"
                (input)="setAlt(image, $event)"
              />
              @if (image.altTextProblem) {
                <p class="field-error" role="alert">{{ image.altTextProblem }}</p>
                <button type="button" class="button button--small" (click)="useSuggestion(image)">Use: "{{ image.suggestedAltText }}"</button>
              }
            </div>
            <div class="form-actions">
              <button type="button" class="button button--small" [disabled]="disabled() || saving()" (click)="saveAlt(image)">Save alt text</button>
              <button type="button" class="button button--small" [disabled]="disabled() || index === 0" (click)="move(index, -1)">Move up</button>
              <button type="button" class="button button--small" [disabled]="disabled() || last" (click)="move(index, 1)">Move down</button>
              @if (confirmingId() === image.id) {
                <button type="button" class="button button--small button--danger" (click)="confirmDelete(image)">Yes, delete it</button>
                <button type="button" class="button button--small" (click)="confirmingId.set(null)">Keep it</button>
              } @else {
                <button type="button" class="button button--small" [disabled]="disabled()" (click)="confirmingId.set(image.id)">Delete</button>
              }
            </div>
          </li>
        }
      </ol>
    }
  `,
  styles: `
    .image-list { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--space-4, 1rem); }
    .image-list__item figure { margin: 0 0 var(--space-3, 0.75rem); }
    .image-list__item img { max-width: 12rem; height: auto; display: block; }
  `,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ImagesPanelComponent {
  readonly images = input.required<readonly ProductImageDto[]>();
  readonly colours = input.required<readonly LookupDto[]>();
  readonly fileUrl = input.required<(imageId: number) => string>();
  /** A published product's new image needs alt text at once (MED-5). */
  readonly requireAlt = input(false);
  readonly saving = input(false);
  readonly disabled = input(false);

  readonly uploaded = output<UploadImageValue>();
  readonly altSaved = output<{ readonly imageId: number; readonly text: string | null }>();
  readonly reordered = output<readonly number[]>();
  readonly deleted = output<number>();

  readonly newAlt = signal('');
  readonly formProblem = signal<string | null>(null);
  readonly confirmingId = signal<number | null>(null);
  private readonly altDrafts = signal<ReadonlyMap<number, string>>(new Map());
  private readonly file = signal<File | null>(null);
  private readonly colourId = signal<number | null>(null);

  readonly count = computed(() => this.images().length);

  constructor() {
    effect(() => {
      const images = this.images();
      untracked(() => {
        this.altDrafts.set(new Map(images.map((image) => [image.id, image.altText ?? ''])));
        this.confirmingId.set(null);
      });
    });
  }

  inputText(event: Event): string {
    return inputValue(event);
  }

  onFile(event: Event): void {
    this.file.set((event.target as HTMLInputElement).files?.item(0) ?? null);
  }

  onColour(event: Event): void {
    this.colourId.set(selectNumberOrNull(event));
  }

  upload(): void {
    const file = this.file();
    if (!file) {
      this.formProblem.set('Choose a picture first.');
      return;
    }

    if (this.requireAlt() && !this.newAlt().trim()) {
      this.formProblem.set('This product is published, so the new image needs alt text that describes it.');
      return;
    }

    this.formProblem.set(null);
    this.uploaded.emit({ file, colourId: this.colourId(), altText: this.newAlt().trim() || null });
    this.newAlt.set('');
  }

  altOf(image: ProductImageDto): string {
    return this.altDrafts().get(image.id) ?? '';
  }

  setAlt(image: ProductImageDto, event: Event): void {
    const next = new Map(this.altDrafts());
    next.set(image.id, inputValue(event));
    this.altDrafts.set(next);
  }

  useSuggestion(image: ProductImageDto): void {
    const next = new Map(this.altDrafts());
    next.set(image.id, image.suggestedAltText);
    this.altDrafts.set(next);
  }

  saveAlt(image: ProductImageDto): void {
    this.altSaved.emit({ imageId: image.id, text: this.altOf(image).trim() || null });
  }

  move(index: number, step: -1 | 1): void {
    const ids = this.images().map((image) => image.id);
    const target = index + step;
    if (target < 0 || target >= ids.length) {
      return;
    }

    [ids[index], ids[target]] = [ids[target], ids[index]];
    this.reordered.emit(ids);
  }

  confirmDelete(image: ProductImageDto): void {
    this.confirmingId.set(null);
    this.deleted.emit(image.id);
  }
}
