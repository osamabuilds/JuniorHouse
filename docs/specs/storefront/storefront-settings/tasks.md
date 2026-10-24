# SCRUM-206: Storefront settings: tasks

- **Spec:** [spec.md](spec.md) · **Plan:** [plan.md](plan.md) · **ADR:** [0020](../../../adr/0020-storefront-settings-and-published-snapshot.md) (plan approved by the owner, 2026-10-24; ADR 0020 to be marked Accepted with it)
- Test-first, tagged with the IDs they cover. Backend tests run in the Linux SDK container (see the docker dotnet test workaround); CI is the authority for the integration tests. The Storefront module is created here.

## A. Module and data
- [x] 1. Create the `Storefront` module projects (`Domain`, `Application`, `Infrastructure`, `IModule`), register it in the host, add `Storefront` to `ModuleNames`. Test: `Romp.ArchitectureTests` pass with the module present.
- [x] 2. Glossary check for every new table and column word (add new abbreviations to `docs/db/naming.md`). Test: the naming architecture test.
- [x] 3. Domain: `NavItem` tree, `AgeStep`, footer, `SettingItem`, `DeliveryRule`, `ExchangePolicy`, `Page`/`PageBlock`, `SettingsDraft`, `PublishedSnapshot`, `SettingsValidator`. Test: unit specs per invariant (depth 3, unique sibling sort, group needs a member, slug pattern and reserved list, fact must exist, size caps) (ST-4, ST-16, ST-17).
- [x] 4. EF configuration for every entity (explicit `ToTable`/`HasColumnName`), lookups seeded, concurrency tokens, indexes on every FK, partial unique index for the current snapshot. Test: naming test and a Testcontainers migration test (ST-16).
- [x] 5. Migration with the launch-set seed (S-1, S-2, WhatsApp, eight page drafts with wording only). Test: migration test reads the seed back; the seed equals the web launch set (contract fixture).

## B. Commands and queries
- [ ] 6. `SaveNavigationCommand` with validator and handler. Test: valid tree saves; fourth level, empty group, duplicate key and dead target are rejected with field messages (ST-1, ST-1a, ST-4).
- [ ] 7. `SaveAgeStepsCommand`, `SaveFooterCommand`, `SaveContactSettingsCommand`, `SaveDeliveryAndExchangeCommand`. Test: handler specs, E.164 phone, email, negative charge rejected, code immutable after publish (ST-2, ST-4, ST-7).
- [ ] 8. Page commands: create, update (blocks), change status, slug change writing a redirect. Test: plain-text blocks, markup stored as text, reserved slug rejected, 301 row created (ST-9, ST-10).
- [ ] 9. `PublishSettingsCommand`: validate the whole draft, write the snapshot and the outbox event in one transaction; same hash returns the existing version. Test: publish makes the set current atomically; a draft edit after publish is not visible; empty fact blocks publish (ST-3, ST-17).
- [ ] 10. `RestoreSnapshotCommand`, `ListSnapshotsQuery` (paged), `PreviewSettingsQuery`, `GetDraftSettingsQuery`. Test: restore creates a new current version; paging order and clamping test (ST-12).
- [ ] 11. Public queries `GetPublishedSettingsQuery` (60 s cache, ETag) and `GetPublishedPageQuery` (200, 404, 410, redirect). Test: cache invalidated by the published event; archived page is Gone (ST-8, ST-13).

## C. API and security
- [ ] 12. Endpoints and the `StorefrontSettings.Admin` policy; start-up check refusing to expose the admin routes in a non-development environment without real authentication. Test: 401 and 403 for every write route; the host refuses to start with the switch on and development authentication outside Development/Test (ST-11).
- [ ] 13. Concurrency: stale row version returns 409. Test: two saves, second rejected (ST-14).
- [ ] 14. Audit via the ADR 0016 interceptor, publish/restore recorded with user and note. Test: an audit record exists for a draft edit and a publish (ST-12).
- [ ] 15. Sitemap source: published pages from the snapshot. Test: a published page appears, an archived one does not (ST-8).

## D. Admin app (`features/storefront/settings`)
- [ ] 16. Feature skeleton: models, `settings-api.service`, store (state, actions, reducer, selectors, effects), routes, admin nav group. Test: reducer and effect specs.
- [ ] 17. Navigation screen: nested tree editor with add member, move up/down, indent/outdent buttons, inline validation. Test: component specs, keyboard-only reordering (ST-1, ST-1a, ST-15).
- [ ] 18. Ages, footer, contact/brand/announcement, delivery and exchange screens. Test: component and form specs with specific error messages (ST-2, ST-4, ST-7).
- [ ] 19. Pages list (paged) and page editor with one editor per block type, fact picker, status control. Test: specs for each block editor and the paged list (ST-8, ST-10, ST-17).
- [ ] 20. Publish screen: preview link, validation report, publish with note, version list (paged), restore. Test: specs for publish and restore flows (ST-3, ST-12).

## E. Wiring and verification
- [ ] 21. Storefront `SiteSettingsApi` points at the real endpoints; the contract fixture test compares the API document with the web launch set. Test: contract spec (ST-1).
- [ ] 22. E2E `src/web/e2e/storefront/settings.e2e.spec.ts`: rename an entry, add a member, publish, see it on the storefront within 60 s; edit a page; archive it and see 410. Test: the spec (ST-1, ST-8).
- [ ] 23. Accessibility pass on the admin screens at 390 px (labels, 44 px targets, button alternatives to drag) with axe. Test: axe spec (ST-15).
- [ ] 24. Close the loop: spec deviations, mark Implemented, ADR 0020 Accepted, READMEs, memory note.

## Implementation notes (deviations from the plan, to fold into the spec in task 24)
- **Concurrency token:** rows use PostgreSQL `xmin` (as every other module does, `docs/db/naming.md` section 2), not a `ROW_VER` column. The draft aggregate also keeps `VER_NO` (ST-14).
- **Sibling positions:** `(PRNT_ID, SORT_SEQ)` is a plain index, not unique: a reorder is saved row by row and a unique check would fail on the intermediate state of a swap. The validator rejects duplicate positions instead.
- **Tables added to the plan's list:** `POPL_SRCH` (popular searches, a SCRUM-205 contract addition), `FACT_LKP` (the six fact keys as a lookup, referenced by `PAGE_BLCK.FACT_ID`), `DRFT_MAIN` (the draft version counter). The redirect table is `PAGE_RDIR`, the link-target lookup `LINK_TGT_LKP`, the current-snapshot column `CRNT_IND` (glossary clashes: `RDIR`, `TGT`, `CURR` = currency).
- **Delivery city:** one `DLVR_RULE` row per city (unique `CITY`); there is no separate city lookup, so a new city is a new row, not a schema change.
- **Code folders:** the API feature folder carries the spec name, `StorefrontSettings`, so `StructureSyncTests` can match it to the spec.
- **Test tooling:** `dotnet ef` runs in the SDK container from the user's global tool store (net8 tool, `DOTNET_ROLL_FORWARD=Major`); the container has no network, so restore uses `-p:NuGetAudit=false -p:RestoreSources=/root/.nuget/packages`.
- **Contract additions found while seeding (task 5), to add to the plan's "Contract additions" and the spec:** a `List` block has an `ordered` flag (`PAGE_BLCK.ORDR_IND`, `ordered` = `ORDR`); a `Table` block's caption is the block `Label`, its header row is row 0 of `PAGE_BLCK_ITM`; `LBL` on a block is 120 characters.
- **Seed vs the web fallback (open, for the owner):** the seeded pages are all `Draft` (task 5: "page drafts"), so the first publish of the seeded draft is refused until the owner fills the facts and publishes the pages (ST-17; the footer links to those pages). The storefront's built-in fallback shows six of them as `published`. They agree on wording, navigation, ages, footer and popular searches (a contract spec checks it); they differ on page status by design, and the fallback should keep serving until the first publish.
- **Seed vs the web model, mapping for the published document (tasks 9 and 11):** a navigation `Link` that has members is emitted as web kind `group` with its target; a domain `Group` (no target) as `group` without one; `CollectionList` as `collection-list`. `brand.whatsapp` is stored as E.164 (`+923315613217`, ST-4) and emitted without the `+` (`923315613217`, the web contract). The footer WhatsApp link needs `wa.me` on the external-host allow-list in configuration.
- **Placeholder facts (owner instruction, 2026-10-24):** the contact email `osamabuss048@gmail.com` is real; opening hours, the four delivery cities (times and charges), the 7-day exchange window and its three conditions are PLACEHOLDERS seeded by `SeedPlaceholderFacts` and must be replaced in the admin app before launch. Pages stay `Draft`, so nothing is published until the owner reviews it.
