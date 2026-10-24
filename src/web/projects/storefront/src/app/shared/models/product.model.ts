export interface ProductSummary {
  readonly slug: string;
  readonly name: string;
  /** Price in PKR. */
  readonly price: number;
  /** Category slug, e.g. `girls`. */
  readonly category: string;
  /** Product type slug, e.g. `dresses`. */
  readonly type: string;
}

export interface ProductSize {
  readonly label: string;
  readonly inStock: boolean;
}

export interface BreadcrumbItem {
  readonly label: string;
  /** Omit on the current page. */
  readonly link?: string;
}
