/** One city's delivery fee (docs/specs/cart/totals, TOT-5). */
export interface ShippingRateDto {
  readonly cityId: number;
  readonly cityName: string;
  readonly provinceName: string;
  readonly fee: number;
}

/** The shop-wide delivery settings. Empty amounts mean "none". `defaultFeeIsUnset` is true while the seeded zero has never been changed. */
export interface ShippingSettingsDto {
  readonly defaultFee: number;
  readonly freeThreshold: number | null;
  readonly codFee: number | null;
  readonly defaultFeeIsUnset: boolean;
}

export interface SettingsValue {
  readonly defaultFee: number;
  readonly freeThreshold: number | null;
  readonly codFee: number | null;
}

export interface ProvinceDto {
  readonly id: number;
  readonly name: string;
}

export interface CityDto {
  readonly id: number;
  readonly name: string;
}
