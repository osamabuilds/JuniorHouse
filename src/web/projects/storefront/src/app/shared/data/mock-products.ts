import { ImageAsset } from '../models/image-asset.model';

/** Placeholder products for suggestions until the search API exists (published-catalog / search features). */
export interface ProductSuggestion {
  readonly id: string;
  readonly name: string;
  readonly price: number;
  readonly image: ImageAsset | null;
}

export const mockProducts: readonly ProductSuggestion[] = [
  { id: 'p1', name: 'Linen button shirt', price: 3200, image: null },
  { id: 'p2', name: 'Cotton dungaree', price: 4100, image: null },
  { id: 'p3', name: 'Floral summer dress', price: 3800, image: null },
  { id: 'p4', name: 'Everyday cotton shorts', price: 1900, image: null },
  { id: 'p5', name: 'Striped t-shirt', price: 1600, image: null },
  { id: 'p6', name: 'Casual check shirt', price: 2900, image: null },
];
