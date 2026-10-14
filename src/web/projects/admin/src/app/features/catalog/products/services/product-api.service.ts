import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiClient } from '@core/http';
import { Page } from '@shared';
import {
  CompositionCareDto,
  CompositionRowInput,
  CreateProductValue,
  PathHistoryDto,
  ProductContentValue,
  ProductDto,
  ProductFilters,
  ProductImageDto,
  ProductPathDto,
  ProductRowDto,
  ProductSizeChartDto,
  PublishProductResult,
  SetPathValue,
  SlugSuggestionDto,
  UploadImageValue,
  VariantPriceInput,
} from '../models';

/** HTTP only, no state (CONVENTIONS section 1). The routes are listed in the plans in docs/specs/catalog. */
@Injectable({ providedIn: 'root' })
export class ProductApiService {
  private readonly api = inject(ApiClient);

  list(filters: ProductFilters): Observable<Page<ProductRowDto>> {
    return this.api.get<Page<ProductRowDto>>('/api/catalog/products', {
      search: filters.search || undefined,
      statusId: filters.statusId,
      page: filters.page,
      pageSize: filters.pageSize,
    });
  }

  get(id: number): Observable<ProductDto> {
    return this.api.get<ProductDto>(`/api/catalog/products/${id}`);
  }

  create(value: CreateProductValue): Observable<ProductDto> {
    return this.api.post<ProductDto>('/api/catalog/products', value);
  }

  saveContent(id: number, value: ProductContentValue, version: number, actingPerson: string): Observable<ProductDto> {
    return this.api.put<ProductDto>(`/api/catalog/products/${id}`, { ...value, version, actingPerson });
  }

  savePrices(id: number, prices: readonly VariantPriceInput[], version: number, actingPerson: string): Observable<ProductDto> {
    return this.api.put<ProductDto>(`/api/catalog/products/${id}/prices`, { prices, version, actingPerson });
  }

  saveCollections(id: number, collectionIds: readonly number[], version: number, actingPerson: string): Observable<ProductDto> {
    return this.api.put<ProductDto>(`/api/catalog/products/${id}/collections`, { collectionIds, version, actingPerson });
  }

  publish(id: number, version: number, actingPerson: string): Observable<PublishProductResult> {
    return this.api.post<PublishProductResult>(`/api/catalog/products/${id}/publish`, { version, actingPerson });
  }

  unpublish(id: number, version: number, actingPerson: string): Observable<ProductDto> {
    return this.api.post<ProductDto>(`/api/catalog/products/${id}/unpublish`, { version, actingPerson });
  }

  archive(id: number, version: number, actingPerson: string): Observable<ProductDto> {
    return this.api.post<ProductDto>(`/api/catalog/products/${id}/archive`, { version, actingPerson });
  }

  refreshVariants(id: number): Observable<ProductDto> {
    return this.api.post<ProductDto>(`/api/catalog/products/${id}/refresh-variants`, {});
  }

  // Images
  images(id: number): Observable<readonly ProductImageDto[]> {
    return this.api.get<readonly ProductImageDto[]>(`/api/catalog/products/${id}/images`);
  }

  uploadImage(id: number, value: UploadImageValue, actingPerson: string): Observable<ProductImageDto> {
    const form = new FormData();
    form.append('actingPerson', actingPerson);
    form.append('file', value.file, value.file.name);
    if (value.colourId !== null) {
      form.append('colourId', String(value.colourId));
    }
    if (value.altText) {
      form.append('altText', value.altText);
    }
    return this.api.postForm<ProductImageDto>(`/api/catalog/products/${id}/images`, form);
  }

  saveAlt(imageId: number, text: string | null, actingPerson: string): Observable<ProductImageDto> {
    return this.api.put<ProductImageDto>(`/api/catalog/images/${imageId}/alt`, { text, actingPerson });
  }

  reorderImages(id: number, imageIds: readonly number[], actingPerson: string): Observable<readonly ProductImageDto[]> {
    return this.api.put<readonly ProductImageDto[]>(`/api/catalog/products/${id}/images/order`, { imageIds, actingPerson });
  }

  deleteImage(imageId: number, actingPerson: string): Observable<void> {
    return this.api.delete<void>(`/api/catalog/images/${imageId}?actingPerson=${encodeURIComponent(actingPerson)}`);
  }

  /** Where an image file is served from (our own record of its type, never the client's). */
  imageFileUrl(imageId: number): string {
    return this.api.url(`/api/catalog/images/${imageId}/file`);
  }

  // Composition and care
  compositionCare(id: number): Observable<CompositionCareDto> {
    return this.api.get<CompositionCareDto>(`/api/catalog/products/${id}/composition-care`);
  }

  saveComposition(id: number, rows: readonly CompositionRowInput[], version: number, actingPerson: string): Observable<CompositionCareDto> {
    return this.api.put<CompositionCareDto>(`/api/catalog/products/${id}/composition`, { rows, version, actingPerson });
  }

  saveCare(id: number, careIds: readonly number[], note: string | null, version: number, actingPerson: string): Observable<CompositionCareDto> {
    return this.api.put<CompositionCareDto>(`/api/catalog/products/${id}/care`, { careIds, note, version, actingPerson });
  }

  // Size chart
  sizeChart(id: number): Observable<ProductSizeChartDto> {
    return this.api.get<ProductSizeChartDto>(`/api/catalog/products/${id}/size-chart`);
  }

  // URLs
  suggestSlug(name: string, genderId: number, categoryId: number, productId: number | null): Observable<SlugSuggestionDto> {
    return this.api.post<SlugSuggestionDto>('/api/catalog/slugs/suggest', { name, genderId, categoryId, productId });
  }

  savePath(id: number, value: SetPathValue, version: number, actingPerson: string): Observable<ProductPathDto> {
    return this.api.put<ProductPathDto>(`/api/catalog/products/${id}/path`, { ...value, version, actingPerson });
  }

  pathHistory(id: number): Observable<readonly PathHistoryDto[]> {
    return this.api.get<readonly PathHistoryDto[]>(`/api/catalog/products/${id}/path-history`);
  }
}
