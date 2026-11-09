import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Store } from '@ngrx/store';
import { holdsAny, selectPermissions } from '@core/auth';
import { ReferenceLookupActions, selectLookup } from '@features/reference/lookups';
import { ActingPersonService, SectionNavComponent, inputValue } from '@shared';
import { CompositionCarePanelComponent } from '../../components/composition-care-panel/composition-care-panel.component';
import { ImagesPanelComponent } from '../../components/images-panel/images-panel.component';
import { ProductContentFormComponent } from '../../components/product-content-form/product-content-form.component';
import { PublishPanelComponent } from '../../components/publish-panel/publish-panel.component';
import { SizeChartViewComponent } from '../../components/size-chart-view/size-chart-view.component';
import { UrlPanelComponent } from '../../components/url-panel/url-panel.component';
import { VariantsTableComponent } from '../../components/variants-table/variants-table.component';
import {
  CompositionRowInput,
  PRODUCT_TABS,
  ProductContentValue,
  ProductImageDto,
  ProductTab,
  SetPathValue,
  UploadImageValue,
  VariantPriceInput,
} from '../../models';
import { CATALOG_LINKS } from '../../products.constants';
import { ProductApiService } from '../../services/product-api.service';
import {
  ProductEditorActions,
  selectChart,
  selectComposition,
  selectEditable,
  selectError,
  selectHistory,
  selectImages,
  selectLoading,
  selectNotice,
  selectProduct,
  selectPublishFailures,
  selectSaving,
  selectSuggestion,
} from '../../store';

const LOOKUP_TYPES = ['genders', 'categories', 'age-brackets', 'collections', 'colours', 'fibres', 'care-instructions'] as const;

/**
 * The product editor (docs/specs/catalog/products/spec.md): one tab per part of the product page, and a Publish tab that lists what is
 * still missing. A save carries the version read with the product, so a second editor is told to reload instead of overwriting (PRD-5).
 */
@Component({
  selector: 'app-product-editor-page',
  imports: [
    RouterLink,
    SectionNavComponent,
    ProductContentFormComponent,
    VariantsTableComponent,
    ImagesPanelComponent,
    SizeChartViewComponent,
    CompositionCarePanelComponent,
    UrlPanelComponent,
    PublishPanelComponent,
  ],
  templateUrl: './product-editor-page.container.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ProductEditorPageContainer {
  private readonly store = inject(Store);
  private readonly actingPerson = inject(ActingPersonService);
  private readonly api = inject(ProductApiService);

  /** The id from the route (<c>catalog/products/:id</c>). */
  readonly id = input.required<string>();

  readonly links = CATALOG_LINKS;
  readonly tabs = PRODUCT_TABS;
  readonly tab = signal<ProductTab>('content');

  readonly product = this.store.selectSignal(selectProduct);
  readonly images = this.store.selectSignal(selectImages);
  readonly composition = this.store.selectSignal(selectComposition);
  readonly chart = this.store.selectSignal(selectChart);
  readonly history = this.store.selectSignal(selectHistory);
  readonly suggestion = this.store.selectSignal(selectSuggestion);
  readonly loading = this.store.selectSignal(selectLoading);
  readonly saving = this.store.selectSignal(selectSaving);
  readonly error = this.store.selectSignal(selectError);
  readonly notice = this.store.selectSignal(selectNotice);
  private readonly productEditable = this.store.selectSignal(selectEditable);
  private readonly permissions = this.store.selectSignal(selectPermissions);
  /** Whether this person may change catalogue content at all; the server checks again on every save (roles AC-7). */
  readonly canWrite = computed(() => holdsAny(this.permissions(), ['catalog.write']));
  readonly editable = computed(() => this.productEditable() && this.canWrite());
  private readonly refusedFailures = this.store.selectSignal(selectPublishFailures);

  readonly genders = this.store.selectSignal(selectLookup('genders'));
  readonly categories = this.store.selectSignal(selectLookup('categories'));
  readonly ageBrackets = this.store.selectSignal(selectLookup('age-brackets'));
  readonly collections = this.store.selectSignal(selectLookup('collections'));
  readonly colours = this.store.selectSignal(selectLookup('colours'));
  readonly fibres = this.store.selectSignal(selectLookup('fibres'));
  readonly careOptions = this.store.selectSignal(selectLookup('care-instructions'));

  readonly person = signal(this.actingPerson.name());
  readonly needName = signal(false);
  readonly fileUrl = (imageId: number) => this.api.imageFileUrl(imageId);

  /** The failures to list: those of the last refused publish, else what the gate says right now. */
  readonly failures = computed(() => this.refusedFailures() ?? this.product()?.gate.failures ?? []);
  readonly conflict = computed(() => this.error()?.status === 409);

  constructor() {
    for (const typeKey of LOOKUP_TYPES) {
      this.store.dispatch(ReferenceLookupActions.requested({ typeKey, includeInactive: false }));
    }

    effect(() => {
      const id = Number(this.id());
      if (Number.isFinite(id)) {
        this.store.dispatch(ProductEditorActions.opened({ id }));
      }
    });
  }

  select(tab: ProductTab): void {
    this.tab.set(tab);
  }

  onName(event: Event): void {
    this.person.set(inputValue(event));
    this.needName.set(false);
  }

  reload(): void {
    this.store.dispatch(ProductEditorActions.opened({ id: Number(this.id()) }));
  }

  dismissNotice(): void {
    this.store.dispatch(ProductEditorActions.noticeDismissed());
  }

  saveContent(value: ProductContentValue): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.contentSaved({ value, person })));
  }

  saveCollections(collectionIds: readonly number[]): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.collectionsSaved({ collectionIds, person })));
  }

  savePrices(prices: readonly VariantPriceInput[]): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.pricesSaved({ prices, person })));
  }

  refreshVariants(): void {
    this.store.dispatch(ProductEditorActions.variantsRefreshClicked());
  }

  upload(value: UploadImageValue): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.imageUploaded({ value, person })));
  }

  saveAlt(change: { readonly imageId: number; readonly text: string | null }): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.altSaved({ ...change, person })));
  }

  reorder(imageIds: readonly number[]): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.imagesReordered({ imageIds, person })));
  }

  deleteImage(imageId: number): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.imageDeleted({ imageId, person })));
  }

  saveComposition(rows: readonly CompositionRowInput[]): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.compositionSaved({ rows, person })));
  }

  saveCare(change: { readonly careIds: readonly number[]; readonly note: string | null }): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.careSaved({ ...change, person })));
  }

  suggestSlug(parts: { readonly genderId: number; readonly categoryId: number }): void {
    const product = this.product();
    if (product) {
      this.store.dispatch(ProductEditorActions.slugSuggestionRequested({ name: product.name, ...parts }));
    }
  }

  savePath(value: SetPathValue): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.pathSaved({ value, person })));
  }

  publish(): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.publishClicked({ person })));
  }

  unpublish(): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.unpublishClicked({ person })));
  }

  archive(): void {
    this.withPerson((person) => this.store.dispatch(ProductEditorActions.archiveClicked({ person })));
  }

  trackImage(_index: number, image: ProductImageDto): number {
    return image.id;
  }

  /** Every change is recorded against a person (until sign-in exists, S8), so the name is asked for before anything is sent. */
  private withPerson(send: (person: string) => void): void {
    const person = this.person().trim();
    if (person.length < 2) {
      this.needName.set(true);
      return;
    }

    this.actingPerson.remember(person);
    send(person);
  }
}
