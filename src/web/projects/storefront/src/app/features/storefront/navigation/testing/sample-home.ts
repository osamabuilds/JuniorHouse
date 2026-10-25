import { Home } from '../models/home.model';

const tile = (n: number) => ({
  path: `/girls/dresses/dress-${n}`,
  name: `Dress ${n}`,
  price: 800,
  compareAtPrice: null,
  savingPercent: 0,
  savingAmount: 0,
  colourCount: 1,
  hasSoldOutSizes: false,
  sizes: [],
  image: { altText: `Dress ${n}`, width: 800, height: 1000, sources: [{ width: 320, webp: '/media/a-320.webp', jpeg: '/media/a-320.jpg' }] },
});

/** A home answer with three arrivals and two audiences (test data only). */
export const sampleHome: Home = {
  newArrivals: [tile(1), tile(2), tile(3)],
  isFallback: false,
  audiences: [
    { code: 'GIRLS', name: 'Girls', path: '/girls' },
    { code: 'BOYS', name: 'Boys', path: '/boys' },
  ],
};
