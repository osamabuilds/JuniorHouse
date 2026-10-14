import { createActionGroup, emptyProps, props } from '@ngrx/store';
import { ApiError } from '@core/http';
import { Page } from '@shared';
import {
  CompositionCareDto,
  CompositionRowInput,
  CreateProductValue,
  PathHistoryDto,
  ProductContentValue,
  ProductDto,
  ProductImageDto,
  ProductRowDto,
  ProductSizeChartDto,
  PublishProductResult,
  SetPathValue,
  SlugSuggestionDto,
  UploadImageValue,
  VariantPriceInput,
} from '../models';

export const ProductsPageActions = createActionGroup({
  source: 'Products Page',
  events: {
    Opened: emptyProps(),
    'Search Changed': props<{ search: string }>(),
    'Status Changed': props<{ statusId: number | null }>(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Refresh Requested': emptyProps(),
    'Create Opened': emptyProps(),
    'Create Closed': emptyProps(),
    'Create Submitted': props<{ value: CreateProductValue }>(),
    'Notice Dismissed': emptyProps(),
  },
});

/** What the person did in the product editor. Every write carries the name of the person acting (until sign-in exists, S8). */
export const ProductEditorActions = createActionGroup({
  source: 'Product Editor',
  events: {
    Opened: props<{ id: number }>(),
    'Content Saved': props<{ value: ProductContentValue; person: string }>(),
    'Prices Saved': props<{ prices: readonly VariantPriceInput[]; person: string }>(),
    'Collections Saved': props<{ collectionIds: readonly number[]; person: string }>(),
    'Publish Clicked': props<{ person: string }>(),
    'Unpublish Clicked': props<{ person: string }>(),
    'Archive Clicked': props<{ person: string }>(),
    'Variants Refresh Clicked': emptyProps(),
    'Image Uploaded': props<{ value: UploadImageValue; person: string }>(),
    'Alt Saved': props<{ imageId: number; text: string | null; person: string }>(),
    'Images Reordered': props<{ imageIds: readonly number[]; person: string }>(),
    'Image Deleted': props<{ imageId: number; person: string }>(),
    'Composition Saved': props<{ rows: readonly CompositionRowInput[]; person: string }>(),
    'Care Saved': props<{ careIds: readonly number[]; note: string | null; person: string }>(),
    'Slug Suggestion Requested': props<{ name: string; genderId: number; categoryId: number }>(),
    'Path Saved': props<{ value: SetPathValue; person: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const ProductsApiActions = createActionGroup({
  source: 'Products API',
  events: {
    'Load List Succeeded': props<{ result: Page<ProductRowDto> }>(),
    'Load List Failed': props<{ error: ApiError }>(),
    'Create Succeeded': props<{ product: ProductDto }>(),
    'Create Failed': props<{ error: ApiError }>(),
    'Load Product Succeeded': props<{ product: ProductDto }>(),
    'Load Product Failed': props<{ error: ApiError }>(),
    /** A command that returns the product: the editor takes it as the new truth and says what happened. */
    'Product Changed': props<{ product: ProductDto; notice: string }>(),
    'Publish Finished': props<{ result: PublishProductResult }>(),
    /** A command whose answer is not the product: the product is read again so its version is current. */
    'Child Changed': props<{ notice: string }>(),
    'Command Failed': props<{ error: ApiError }>(),
    'Images Loaded': props<{ images: readonly ProductImageDto[] }>(),
    'Composition Loaded': props<{ composition: CompositionCareDto }>(),
    'Chart Loaded': props<{ chart: ProductSizeChartDto }>(),
    'History Loaded': props<{ history: readonly PathHistoryDto[] }>(),
    'Suggestion Loaded': props<{ suggestion: SlugSuggestionDto }>(),
  },
});
