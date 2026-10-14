/** One planned size × colour with its price and the barcode facts Labelling holds (docs/specs/catalog/products/spec.md, PRD-1). */
export type VariantAvailability = 'Sellable' | 'Unpriced' | 'GtinVoided' | 'NotYetAvailable';

export interface VariantDto {
  readonly id: number;
  readonly colourId: number;
  readonly colourName: string;
  readonly sizeId: number;
  readonly sizeName: string;
  readonly price: number | null;
  readonly compareAtPrice: number | null;
  readonly skuCode: string | null;
  readonly gtin: string | null;
  readonly availability: VariantAvailability;
  readonly isSellable: boolean;
}

export type ProductTab = 'content' | 'variants' | 'images' | 'size-chart' | 'composition' | 'url' | 'publish';

/** One thing stopping a publish, with the tab that fixes it (PRD-4). */
export interface GateFailureDto {
  readonly code: string;
  readonly message: string;
  readonly tab: ProductTab;
}

export interface PublishGatePreviewDto {
  readonly failures: readonly GateFailureDto[];
  readonly warnings: readonly string[];
}

/** The product tabs in the order they appear; the gate's `tab` values are among them. */
export const PRODUCT_TABS: readonly { readonly key: ProductTab; readonly label: string }[] = [
  { key: 'content', label: 'Content' },
  { key: 'variants', label: 'Variants and prices' },
  { key: 'images', label: 'Images' },
  { key: 'size-chart', label: 'Size chart' },
  { key: 'composition', label: 'Composition and care' },
  { key: 'url', label: 'URL' },
  { key: 'publish', label: 'Publish' },
];

export interface ProductDto {
  readonly id: number;
  readonly styleId: number;
  readonly styleCode: string;
  readonly name: string;
  readonly description: string;
  readonly genderId: number;
  readonly categoryId: number;
  readonly ageBracketId: number;
  readonly statusId: number;
  readonly status: 'Draft' | 'Published' | 'Unpublished' | 'Archived';
  readonly slug: string;
  readonly path: string;
  readonly careNote: string | null;
  readonly publishedAt: string | null;
  readonly publishedBy: string | null;
  readonly version: number;
  readonly collectionIds: readonly number[];
  readonly variants: readonly VariantDto[];
  readonly sellableVariants: number;
  readonly gate: PublishGatePreviewDto;
}

export interface ProductRowDto {
  readonly id: number;
  readonly styleId: number;
  readonly styleCode: string;
  readonly name: string;
  readonly genderId: number;
  readonly categoryId: number;
  readonly statusId: number;
  readonly status: string;
  readonly slug: string;
  readonly plannedVariants: number;
  readonly sellableVariants: number;
  readonly publishedAt: string | null;
}

export interface ProductFilters {
  readonly search: string;
  readonly statusId: number | null;
  readonly page: number;
  readonly pageSize: number;
}

export interface PublishProductResult {
  readonly published: boolean;
  readonly product: ProductDto;
  readonly failures: readonly GateFailureDto[];
}

export interface CreateProductValue {
  readonly styleId: number;
  readonly name: string;
  readonly description: string;
  readonly genderId: number;
  readonly categoryId: number;
  readonly ageBracketId: number;
  readonly actingPerson: string;
}

export interface ProductContentValue {
  readonly name: string;
  readonly description: string;
  readonly genderId: number;
  readonly categoryId: number;
  readonly ageBracketId: number;
  readonly careNote: string | null;
}

export interface VariantPriceInput {
  readonly colourId: number;
  readonly sizeId: number;
  readonly price: number;
  readonly compareAtPrice: number | null;
}

/** The statuses the list can filter by (the PRDT_STS_LKP rows). */
export const PRODUCT_STATUSES: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Draft' },
  { id: 2, label: 'Published' },
  { id: 3, label: 'Unpublished' },
  { id: 4, label: 'Archived' },
];

// Images (docs/specs/catalog/product-media/spec.md)
export interface ProductImageDto {
  readonly id: number;
  readonly productId: number;
  readonly colourId: number | null;
  readonly fileName: string;
  readonly contentType: string;
  readonly byteSize: number;
  readonly width: number;
  readonly height: number;
  readonly sortSeq: number;
  readonly isMain: boolean;
  readonly altText: string | null;
  readonly suggestedAltText: string;
  readonly altTextProblem: string | null;
}

export interface UploadImageValue {
  readonly file: File;
  readonly colourId: number | null;
  readonly altText: string | null;
}

// Composition and care (docs/specs/catalog/composition-care/spec.md)
export interface CompositionRowInput {
  readonly fibreId: number;
  readonly percent: number;
}

export interface CompositionRowDto extends CompositionRowInput {
  readonly fibreName: string;
}

export interface CareInstructionDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly symbolCode: string;
}

export interface CompositionCareDto {
  readonly productId: number;
  readonly rows: readonly CompositionRowDto[];
  readonly total: number;
  readonly isComplete: boolean;
  readonly care: readonly CareInstructionDto[];
  readonly careNote: string | null;
  readonly version: number;
}

// Size chart of a product (docs/specs/catalog/size-charts/spec.md)
export interface SizeChartLineDto {
  readonly sizeId: number;
  readonly sizeName: string;
  readonly ageMinMonths: number | null;
  readonly ageMaxMonths: number | null;
  readonly heightMinCm: number | null;
  readonly heightMaxCm: number | null;
  readonly weightMinKg: number | null;
  readonly weightMaxKg: number | null;
  readonly chestCm: number | null;
  readonly waistCm: number | null;
  readonly lengthCm: number | null;
}

export interface ProductSizeChartDto {
  readonly found: boolean;
  readonly chartId: number | null;
  readonly name: string | null;
  readonly unit: string;
  readonly lines: readonly SizeChartLineDto[];
}

// URLs (docs/specs/catalog/urls/spec.md)
export interface SlugSuggestionDto {
  readonly slug: string;
  readonly path: string;
}

export interface PathHistoryDto {
  readonly path: string;
  readonly isCurrent: boolean;
  readonly firstUsedAt: string;
}

export interface ProductPathDto {
  readonly productId: number;
  readonly genderId: number;
  readonly categoryId: number;
  readonly slug: string;
  readonly path: string;
  readonly version: number;
}

export interface SetPathValue {
  readonly genderId: number;
  readonly categoryId: number;
  readonly slug: string;
}
