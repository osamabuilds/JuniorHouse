The index of collections, reached from the header's Collections entry. Collections are data (a lookup), so the page lists whatever is enabled in the settings; names and counts below are placeholders.

- **Layout.** Breadcrumb, `display-lg` "Collections", a one-line lead, and a two-up grid of `rp-tile`s (image, scrim, eyebrow, `display-md` name, "[N] pieces"). On a phone the tiles stack. One link per tile.
- A collection opens its listing (`/collections/<code>`), which is the standard listing page with the collection's description.

Rules: only collections with at least one live product are listed; an enabled collection with none shows the listing empty state; the page is indexable with a unique title and description, `BreadcrumbList` JSON-LD and a canonical (SEO-03, SEO-04). Placeholders: collection names, photography and counts.
