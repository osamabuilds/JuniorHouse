/** The shapes GET /api/storefront/products/by-path returns. Amounts and availability words are the server's; nothing is computed here (SEC-09). */

export interface ProductImageSource {
  readonly width: number;
  readonly webp: string;
  readonly jpeg: string | null;
}

export interface ProductImage {
  readonly altText: string;
  readonly width: number;
  readonly height: number;
  readonly sources: readonly ProductImageSource[];
}

export interface ColourOption {
  readonly code: string;
  readonly name: string;
  readonly inStock: boolean;
}

export type SizeAvailability = 'InStock' | 'LowStock' | 'SoldOut' | 'NotYetAvailable';

export interface SizeOption {
  readonly code: string;
  readonly name: string;
  readonly availability: SizeAvailability;
  readonly selectable: boolean;
}

export interface ProductPrice {
  readonly price: number;
  readonly compareAtPrice: number | null;
  readonly savingPercent: number;
  /** The server difference in PKR (SEC-09); 0 when there is no discount. */
  readonly savingAmount: number;
  /** True before a size is chosen when sizes differ in price: shown as "From". */
  readonly isFrom: boolean;
}

export interface CompositionLine {
  readonly name: string;
  readonly percent: number;
}

export interface CareLine {
  readonly name: string;
  readonly symbolCode: string;
}

export interface SizeChartRow {
  readonly sizeCode: string;
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
  readonly selected: boolean;
}

export interface SizeChart {
  readonly name: string;
  readonly rows: readonly SizeChartRow[];
}

export interface Breadcrumb {
  readonly label: string;
  readonly path: string | null;
}

export interface ProductPage {
  readonly path: string;
  readonly name: string;
  readonly description: string;
  readonly audienceName: string;
  readonly categoryName: string;
  readonly ageName: string | null;
  readonly breadcrumbs: readonly Breadcrumb[];
  readonly price: ProductPrice | null;
  readonly selectedColour: string;
  readonly selectedSize: string | null;
  readonly sku: string | null;
  readonly colours: readonly ColourOption[];
  readonly sizes: readonly SizeOption[];
  readonly images: readonly ProductImage[];
  readonly sizeChart: SizeChart | null;
  readonly composition: readonly CompositionLine[];
  readonly care: readonly CareLine[];
  readonly careNote: string | null;
  readonly isAvailable: boolean;
}

export type ProductOutcome = 'Found' | 'Moved' | 'Gone' | 'NotFound';

export interface ProductPageResponse {
  readonly outcome: ProductOutcome;
  readonly path: string | null;
  readonly page: ProductPage | null;
}

/** What the page shows once a request has finished. `error` is a failed call (503), not a missing page. */
export type ProductPageResult =
  | { readonly status: 'found'; readonly page: ProductPage }
  | { readonly status: 'moved'; readonly path: string }
  | { readonly status: 'gone' }
  | { readonly status: 'notFound' }
  | { readonly status: 'error' };
