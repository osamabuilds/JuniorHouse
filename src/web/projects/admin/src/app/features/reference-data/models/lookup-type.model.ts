export interface LookupTypeConfig {
  readonly key: string;
  readonly label: string;
  readonly mutable: boolean;
  readonly hasParent?: boolean;
  readonly hasAdvancePercent?: boolean;
}
