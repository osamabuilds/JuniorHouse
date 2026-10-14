import { LookupDto } from '@features/reference/lookups';
import {
  CompositionCareDto,
  ProductDto,
  ProductImageDto,
  ProductRowDto,
  ProductSizeChartDto,
  VariantDto,
} from '@features/catalog/products';

export function lookup(id: number, name: string, code = name.toUpperCase()): LookupDto {
  return { id, code, name, description: null, sortSeq: id, isActive: true, parentCategoryId: null, defaultAdvancePercent: null };
}

export function variant(overrides: Partial<VariantDto> = {}): VariantDto {
  return {
    id: 1,
    colourId: 1,
    colourName: 'Red',
    sizeId: 4,
    sizeName: '1-2 Years',
    price: 1200,
    compareAtPrice: null,
    skuCode: 'ROMP-STY-DRESS-RED-1-2Y',
    gtin: '0200000000019',
    availability: 'Sellable',
    isSellable: true,
    ...overrides,
  };
}

export function product(overrides: Partial<ProductDto> = {}): ProductDto {
  return {
    id: 7,
    styleId: 3,
    styleCode: 'STY-DRESS-001',
    name: 'Floral Summer Dress',
    description: 'A soft cotton dress.',
    genderId: 2,
    categoryId: 3,
    ageBracketId: 4,
    statusId: 1,
    status: 'Draft',
    slug: 'floral-summer-dress',
    path: '/girls/dresses/floral-summer-dress',
    careNote: null,
    publishedAt: null,
    publishedBy: null,
    version: 100,
    collectionIds: [],
    variants: [variant(), variant({ id: 2, sizeId: 5, sizeName: '2-3 Years', skuCode: null, gtin: null, availability: 'NotYetAvailable', isSellable: false })],
    sellableVariants: 1,
    gate: { failures: [], warnings: [] },
    ...overrides,
  };
}

export function productRow(overrides: Partial<ProductRowDto> = {}): ProductRowDto {
  return {
    id: 7,
    styleId: 3,
    styleCode: 'STY-DRESS-001',
    name: 'Floral Summer Dress',
    genderId: 2,
    categoryId: 3,
    statusId: 1,
    status: 'Draft',
    slug: 'floral-summer-dress',
    plannedVariants: 6,
    sellableVariants: 2,
    publishedAt: null,
    ...overrides,
  };
}

export function image(overrides: Partial<ProductImageDto> = {}): ProductImageDto {
  return {
    id: 11,
    productId: 7,
    colourId: null,
    fileName: 'front.png',
    contentType: 'image/png',
    byteSize: 1024,
    width: 640,
    height: 480,
    sortSeq: 1,
    isMain: true,
    altText: 'A red floral dress, front view',
    suggestedAltText: 'Floral Summer Dress, view 1',
    altTextProblem: null,
    ...overrides,
  };
}

export function compositionCare(overrides: Partial<CompositionCareDto> = {}): CompositionCareDto {
  return {
    productId: 7,
    rows: [
      { fibreId: 1, fibreName: 'Cotton', percent: 95 },
      { fibreId: 3, fibreName: 'Elastane', percent: 5 },
    ],
    total: 100,
    isComplete: true,
    care: [{ id: 1, code: 'WASH-30', name: 'Machine wash at 30 C', symbolCode: 'wash-30' }],
    careNote: null,
    version: 100,
    ...overrides,
  };
}

export function chart(overrides: Partial<ProductSizeChartDto> = {}): ProductSizeChartDto {
  return {
    found: true,
    chartId: 4,
    name: 'Girls dresses',
    unit: 'cm',
    lines: [
      {
        sizeId: 4,
        sizeName: '1-2 Years',
        ageMinMonths: 12,
        ageMaxMonths: 24,
        heightMinCm: 80,
        heightMaxCm: 92,
        weightMinKg: null,
        weightMaxKg: null,
        chestCm: 52,
        waistCm: null,
        lengthCm: null,
      },
    ],
    ...overrides,
  };
}
