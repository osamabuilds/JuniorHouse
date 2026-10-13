/** Key of one cached lookup list: the same type is cached separately with and without retired values. */
export function lookupCacheKey(typeKey: string, includeInactive: boolean): string {
  return includeInactive ? `${typeKey}:all` : typeKey;
}
