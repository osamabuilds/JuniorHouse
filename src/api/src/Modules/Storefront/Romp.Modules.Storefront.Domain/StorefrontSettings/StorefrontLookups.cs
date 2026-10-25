namespace Romp.Modules.Storefront.Domain.StorefrontSettings;

/// <summary>STFR.NAV_KIND_LKP</summary>
public sealed class NavKindLookup : StorefrontLookup<NavKind>;

/// <summary>STFR.LINK_TGT_LKP</summary>
public sealed class LinkTargetLookup : StorefrontLookup<LinkTargetKind>;

/// <summary>STFR.STNG_KEY_LKP</summary>
public sealed class SettingKeyLookup : StorefrontLookup<SettingKey>;

/// <summary>STFR.PAGE_STS_LKP</summary>
public sealed class PageStatusLookup : StorefrontLookup<PageStatus>;

/// <summary>STFR.BLCK_TYP_LKP</summary>
public sealed class BlockTypeLookup : StorefrontLookup<BlockType>;

/// <summary>STFR.FACT_LKP: the facts a page may quote (<see cref="SettingsFacts.Keys"/>), keyed by <see cref="SettingsFacts.IdOf"/>.</summary>
public sealed class FactLookup : StorefrontLookup<short>;
